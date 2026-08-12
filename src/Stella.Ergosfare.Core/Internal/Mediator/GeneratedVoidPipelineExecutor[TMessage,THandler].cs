using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Void pipeline closed over both the message and its compile-time-known sole handler —
/// the executor a generated void plan constructs. The fast path resolves the handler
/// reference exactly like <see cref="VoidPipelineExecutor{TMessage}"/> but invokes it
/// through the closed <typeparamref name="THandler"/> type, so the call devirtualizes
/// (and inlines for sealed handlers) instead of walking the contract pattern match. The
/// plan is advisory: the same registry-version-guarded dependency cache re-validates the
/// pipeline, and any mismatch — interceptors registered at runtime, a differently-typed
/// handler instance, configured adapters — falls back to the runtime dispatch shape,
/// preserving semantics exactly.
/// </summary>
internal sealed class GeneratedVoidPipelineExecutor<TMessage, THandler>(
    IMessageDependenciesFactory dependenciesFactory,
    string[] groups,
    Func<THandler>? directHandlerFactory = null,
    Func<IServiceProvider, THandler>? providerHandlerFactory = null) : IPipelineExecutor
    where TMessage : IMessage
    where THandler : class, IAsyncHandler<TMessage>
{
    private readonly SingleAsyncHandlerMediationStrategy<TMessage> _strategy = new();

    // Whether the pipeline's Unit slot has an effective adapter — the attribute tiers
    // plus the container's default, resolved once on the first dispatch, so the fast
    // paths below pay nothing when (as almost always) there is none.
    private bool _hasResultAdapter;
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

    public ValueTask Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider)
    {
        if (!_resultAdapterResolved)
        {
            _hasResultAdapter = global::Stella.Ergosfare.Core.Abstractions.Results
                .ResultAdapterBinding.For<TMessage, Unit>(serviceProvider) is not null;
            _resultAdapterResolved = true;
        }

        var dependencies = GetDependencies();

        if (_cachedFastDependencies?.FastSingleHandler is { } handlerReference
            && !_hasResultAdapter)
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
            // The strategy is skipped here, so its abort handling has to be too; see
            // AbortShortCircuit.
            try
            {
                if (handler is THandler planned)
                {
                    return AbortShortCircuit.Guard(planned.HandleAsync((TMessage)message, context));
                }

                switch (handler)
                {
                    case IAsyncHandler<TMessage> asyncHandler:
                        return AbortShortCircuit.Guard(asyncHandler.HandleAsync((TMessage)message, context));
                    case IHandler<TMessage, ValueTask> valueTaskShaped:
                        return AbortShortCircuit.Guard(valueTaskShaped.Handle((TMessage)message, context));
                    case IHandler<TMessage, object> syncHandler:
                        syncHandler.Handle((TMessage)message, context);
                        return ValueTask.CompletedTask;
                }
            }
            catch (ExecutionAbortedException)
            {
                return ValueTask.CompletedTask;
            }
        }

        return _strategy.Mediate((TMessage)message, dependencies, context, serviceProvider);
    }

    private IMessageDependencies GetDependencies()
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            if (_cachedDependencies is { } cached)
            {
                return cached;
            }

            var dependencies = typedFactory.Create(typeof(TMessage), groups);
            var fastDependencies = dependencies as MessageDependencies;
            _cachedFastDependencies = fastDependencies;
            _cachedDependencies = dependencies;
            _useDirectConstruction = (_directHandlerFactory is not null || _providerHandlerFactory is not null)
                && fastDependencies is { MemoizedInstances: false, FastSingleHandler.HandlerType: var plannedType }
                && plannedType == typeof(THandler)
                && typedFactory.IsPlainTransientRegistration(typeof(THandler));
            return dependencies;
        }

        _useDirectConstruction = false;
        return dependenciesFactory.Create(typeof(TMessage), groups);
    }
}
