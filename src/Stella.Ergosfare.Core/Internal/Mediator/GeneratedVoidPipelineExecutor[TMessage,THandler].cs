using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Results;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Void pipeline closed over both the message and its compile-time-known sole handler —
/// the executor a generated void plan constructs. The fast path resolves the handler
/// reference exactly like <see cref="VoidPipelineExecutor{TMessage}"/> but invokes it
/// through the closed <typeparamref name="THandler"/> type, so the call devirtualizes
/// (and inlines for sealed handlers) instead of walking the contract pattern match. The
/// plan is advisory: the dependency cache validates the pipeline on the first dispatch,
/// and any mismatch — a differently-typed handler instance, configured adapters — falls
/// back to the runtime dispatch shape, preserving semantics exactly. The validated shape
/// is frozen; a registration after the first dispatch is not observed.
/// </summary>
internal sealed class GeneratedVoidPipelineExecutor<TMessage, THandler>(
    IMessageDependenciesFactory dependenciesFactory,
    Func<THandler>? directHandlerFactory = null,
    Func<IServiceProvider, THandler>? providerHandlerFactory = null) : IPipelineExecutor
    where TMessage : IMessage
    where THandler : class, IAsyncHandler<TMessage>
{
    private static readonly string[] EmptyGroups = [];

    /// <summary>
    /// The grouped compositions of this pipeline. The plan names one handler as the whole
    /// pipeline; a filter may exclude that handler or admit another, so a filtered
    /// dispatch selects its own frozen composition and runs the runtime body.
    /// </summary>
    private readonly GroupedCompositions _grouped = new(dependenciesFactory, typeof(TMessage));

    // The effective adapter of the pipeline's Unit slot — the attribute tiers plus the
    // container's default, resolved once on the first dispatch, so the fast paths below
    // pay nothing when (as almost always) there is none.
    private IResultAdapter<Unit>? _resultAdapter;
    private IResultMaterializer<Unit>? _resultMaterializer;
    private volatile bool _resultAdapterResolved;

    // Compile-time construction paths for the planned handler; discarded up front for
    // disposable handlers — the container tracks transient disposables in the resolving
    // scope, direct construction would not. The provider-taking shape covers handlers
    // with constructor dependencies: it resolves them from the dispatching scope's
    // provider, exactly where container activation would resolve them.
    private static readonly bool HandlerIsDisposable =
        typeof(IDisposable).IsAssignableFrom(typeof(THandler)) || typeof(IAsyncDisposable).IsAssignableFrom(typeof(THandler));

    private readonly Func<THandler>? _directHandlerFactory = HandlerIsDisposable ? null : directHandlerFactory;
    private readonly Func<IServiceProvider, THandler>? _providerHandlerFactory = HandlerIsDisposable ? null : providerHandlerFactory;

    private IMessageDependencies? _cachedDependencies;
    private MessageDependencies? _cachedFastDependencies;

    // Re-validated with the dependency cache: true only while the registry's sole handler
    // is the planned type, instances are not memoized, and the handler's effective DI
    // registration is the module's own plain transient one — the exact conditions under
    // which GetRequiredService is observably nothing but a constructor call.
    private bool _useDirectConstruction;

    // True once the first dispatch has validated the entire fast lane — planned handler
    // type, direct construction, no adapters. From then on the planned handler is
    // constructed and invoked without touching dependencies at all.
    private bool _fastDirect;

    public ValueTask Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups)
    {
        if (groups is not null)
        {
            return ExecuteGrouped(message, context, serviceProvider, groups);
        }

        if (_fastDirect)
        {
            var direct = _directHandlerFactory is not null ? _directHandlerFactory() : _providerHandlerFactory!(serviceProvider);
            return direct.HandleAsync((TMessage)message, context);
        }

        EnsureResultAdapter(serviceProvider);

        var dependencies = GetDependencies();

        if (_cachedFastDependencies?.FastSingleHandler is { } handlerReference
            && _resultAdapter is null)
        {
            // The handler-type re-check pins the racy flag to the reference actually in
            // hand: a version transition observed halfway can only route back through the
            // container, never construct a type the registry no longer plans.
            IHandler handler = _useDirectConstruction && handlerReference.HandlerType == typeof(THandler)
                ? _directHandlerFactory is not null ? _directHandlerFactory() : _providerHandlerFactory!(serviceProvider)
                : handlerReference.Resolve(serviceProvider);

            // The compile-time plan's handler type: a devirtualized call, no pattern
            // match. A runtime re-registration can put a differently-typed handler here;
            // the contract switch below then dispatches it exactly as the runtime
            // executor would.
            // The strategy is skipped here, and with it its abort handling. That arm lives
            // in the engine's dispatch frame — an exception-handling region here would keep
            // Execute out of its caller on every dispatch; see MessageDispatchEngine.
            if (handler is THandler planned)
            {
                return planned.HandleAsync((TMessage)message, context);
            }

            switch (handler)
            {
                case IAsyncHandler<TMessage> asyncHandler:
                    return asyncHandler.HandleAsync((TMessage)message, context);
                case IHandler<TMessage, ValueTask> valueTaskShaped:
                    return valueTaskShaped.Handle((TMessage)message, context);
                case IHandler<TMessage, object> syncHandler:
                    syncHandler.Handle((TMessage)message, context);
                    return ValueTask.CompletedTask;
            }
        }

        return VoidPipelineBody<TMessage>.Run(
            (TMessage)message, dependencies, _resultAdapter, _resultMaterializer, context, serviceProvider);
    }

    /// <summary>
    /// The group-filtered dispatch: the composition the filter selects, delivered by the
    /// runtime lane — the plan named one handler for the unfiltered pipeline and has
    /// nothing to say about a filtered one.
    /// </summary>
    private ValueTask ExecuteGrouped(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string> groups)
    {
        EnsureResultAdapter(serviceProvider);

        var composition = _grouped.Resolve(groups);

        if (composition.Fast?.FastSingleHandler is { } handlerReference && _resultAdapter is null)
        {
            var handler = handlerReference.Resolve(serviceProvider);

            switch (handler)
            {
                case IAsyncHandler<TMessage> asyncHandler:
                    return asyncHandler.HandleAsync((TMessage)message, context);
                case IHandler<TMessage, ValueTask> valueTaskShaped:
                    return valueTaskShaped.Handle((TMessage)message, context);
                case IHandler<TMessage, object> syncHandler:
                    syncHandler.Handle((TMessage)message, context);
                    return ValueTask.CompletedTask;
            }
        }

        return VoidPipelineBody<TMessage>.Run(
            (TMessage)message, composition.Dependencies, _resultAdapter, _resultMaterializer, context, serviceProvider);
    }

    /// <summary>
    /// Resolves the slot's effective adapter once; see
    /// <see cref="FrozenVoidDispatch{TMessage}.EnsureResultAdapter"/>.
    /// </summary>
    private void EnsureResultAdapter(IServiceProvider serviceProvider)
    {
        if (_resultAdapterResolved)
        {
            return;
        }

        var adapter = ResultAdapterBinding.For<TMessage, Unit>(serviceProvider);
        _resultAdapter = adapter;
        _resultMaterializer = adapter as IResultMaterializer<Unit>;
        _resultAdapterResolved = true;
    }

    private IMessageDependencies GetDependencies()
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            // Frozen registry: dependencies resolve once per executor and are never
            // re-validated — a registration after the first dispatch is not observed.
            var cached = _cachedDependencies;

            if (cached is not null)
            {
                return cached;
            }

            var dependencies = typedFactory.Create(typeof(TMessage), EmptyGroups);
            var fastDependencies = dependencies as MessageDependencies;
            _cachedFastDependencies = fastDependencies;
            _cachedDependencies = dependencies;
            _useDirectConstruction = (_directHandlerFactory is not null || _providerHandlerFactory is not null)
                && fastDependencies is { MemoizedInstances: false, FastSingleHandler.HandlerType: var plannedType }
                && plannedType == typeof(THandler)
                && typedFactory.IsPlainTransientRegistration(typeof(THandler));
            // Execute resolves the adapter slot before the first GetDependencies call, so
            // the answer is already in hand here.
            _fastDirect = _useDirectConstruction && _resultAdapter is null;
            return dependencies;
        }

        _useDirectConstruction = false;
        return dependenciesFactory.Create(typeof(TMessage), EmptyGroups);
    }
}
