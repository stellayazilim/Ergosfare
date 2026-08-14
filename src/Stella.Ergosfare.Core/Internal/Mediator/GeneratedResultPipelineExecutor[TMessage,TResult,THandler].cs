using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Results;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Result-producing counterpart of
/// <see cref="GeneratedVoidPipelineExecutor{TMessage, THandler}"/>: closed over the
/// message, its result and its compile-time-known sole async handler, so the handler call
/// devirtualizes instead of walking the contract pattern match. The same advisory-plan
/// contract applies — the dependency cache validates the pipeline on the first dispatch
/// and any mismatch falls back to the runtime dispatch shape; the validated shape is
/// frozen thereafter.
/// </summary>
#pragma warning disable CS8714 // TResult is used as a pattern type argument; handler contracts declare notnull results
internal sealed class GeneratedResultPipelineExecutor<TMessage, TResult, THandler>(
    IMessageDependenciesFactory dependenciesFactory,
    Func<THandler>? directHandlerFactory = null,
    Func<IServiceProvider, THandler>? providerHandlerFactory = null) : IPipelineExecutor<TResult>
    where TMessage : IMessage
    where THandler : class, IAsyncHandler<TMessage, TResult>
{
    private static readonly string[] EmptyGroups = [];

    /// <summary>
    /// The grouped compositions of this pipeline; see
    /// <see cref="GeneratedVoidPipelineExecutor{TMessage, THandler}"/>.
    /// </summary>
    private readonly GroupedCompositions _grouped = new(dependenciesFactory, typeof(TMessage));

    // The effective adapter of the pipeline's result slot — the attribute tiers plus the
    // container's default, resolved once on the first dispatch, so the fast paths below
    // pay nothing when (as almost always) there is none.
    private IResultAdapter<TResult>? _resultAdapter;
    private IResultMaterializer<TResult>? _resultMaterializer;
    private volatile bool _resultAdapterResolved;

    private static readonly bool HandlerIsDisposable =
        typeof(IDisposable).IsAssignableFrom(typeof(THandler)) || typeof(IAsyncDisposable).IsAssignableFrom(typeof(THandler));

    private readonly Func<THandler>? _directHandlerFactory = HandlerIsDisposable ? null : directHandlerFactory;
    private readonly Func<IServiceProvider, THandler>? _providerHandlerFactory = HandlerIsDisposable ? null : providerHandlerFactory;

    private IMessageDependencies? _cachedDependencies;
    private MessageDependencies? _cachedFastDependencies;
    private bool _useDirectConstruction;

    // True once the first dispatch has validated the entire fast lane — planned handler
    // type, direct construction, no adapters. From then on the planned handler is
    // constructed and invoked without touching dependencies at all.
    private bool _fastDirect;

    public ValueTask<TResult> Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider,
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
            IHandler handler = _useDirectConstruction && handlerReference.HandlerType == typeof(THandler)
                ? _directHandlerFactory is not null ? _directHandlerFactory() : _providerHandlerFactory!(serviceProvider)
                : handlerReference.Resolve(serviceProvider);

            // The strategy is skipped here, and with it its abort handling. That arm lives
            // in the engine's dispatch frame — an exception-handling region here would keep
            // Execute out of its caller on every dispatch; see MessageDispatchEngine.
            if (handler is THandler planned)
            {
                return planned.HandleAsync((TMessage)message, context);
            }

            switch (handler)
            {
                case IAsyncHandler<TMessage, TResult> asyncHandler:
                    return asyncHandler.HandleAsync((TMessage)message, context);
                case IHandler<TMessage, ValueTask<TResult>> valueTaskShaped:
                    return valueTaskShaped.Handle((TMessage)message, context);
                case IHandler<TMessage, TResult> syncHandler:
                    return ValueTask.FromResult(syncHandler.Handle((TMessage)message, context));
            }
        }

        return ResultPipelineBody<TMessage, TResult>.Run(
            (TMessage)message, dependencies, _resultAdapter, _resultMaterializer, context, serviceProvider);
    }

    /// <summary>
    /// The group-filtered dispatch; see
    /// <see cref="GeneratedVoidPipelineExecutor{TMessage, THandler}"/>.
    /// </summary>
    private ValueTask<TResult> ExecuteGrouped(object message, ErgosfareContext context,
        IServiceProvider serviceProvider, IEnumerable<string> groups)
    {
        EnsureResultAdapter(serviceProvider);

        var composition = _grouped.Resolve(groups);

        if (composition.Fast?.FastSingleHandler is { } handlerReference && _resultAdapter is null)
        {
            var handler = handlerReference.Resolve(serviceProvider);

            switch (handler)
            {
                case IAsyncHandler<TMessage, TResult> asyncHandler:
                    return asyncHandler.HandleAsync((TMessage)message, context);
                case IHandler<TMessage, ValueTask<TResult>> valueTaskShaped:
                    return valueTaskShaped.Handle((TMessage)message, context);
                case IHandler<TMessage, TResult> syncHandler:
                    return ValueTask.FromResult(syncHandler.Handle((TMessage)message, context));
            }
        }

        return ResultPipelineBody<TMessage, TResult>.Run(
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

        var adapter = ResultAdapterBinding.For<TMessage, TResult>(serviceProvider);
        _resultAdapter = adapter;
        _resultMaterializer = adapter as IResultMaterializer<TResult>;
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
#pragma warning restore CS8714
