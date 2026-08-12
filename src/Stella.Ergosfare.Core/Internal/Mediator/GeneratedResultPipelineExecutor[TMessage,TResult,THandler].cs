using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Result-producing counterpart of
/// <see cref="GeneratedVoidPipelineExecutor{TMessage, THandler}"/>: closed over the
/// message, its result and its compile-time-known sole async handler, so the handler call
/// devirtualizes instead of walking the contract pattern match. The same advisory-plan
/// contract applies — the registry-version-guarded dependency cache re-validates the
/// pipeline and any mismatch falls back to the runtime dispatch shape.
/// </summary>
#pragma warning disable CS8714 // TResult is used as a pattern type argument; handler contracts declare notnull results
internal sealed class GeneratedResultPipelineExecutor<TMessage, TResult, THandler>(
    IMessageDependenciesFactory dependenciesFactory,
    string[] groups,
    Func<THandler>? directHandlerFactory = null,
    Func<IServiceProvider, THandler>? providerHandlerFactory = null) : IPipelineExecutor<TResult>
    where TMessage : IMessage
    where THandler : class, IAsyncHandler<TMessage, TResult>
{
    private readonly SingleAsyncHandlerMediationStrategy<TMessage, TResult> _strategy = new();

    // Whether the pipeline's result slot has an effective adapter — the attribute tiers
    // plus the container's default, resolved once on the first dispatch, so the fast
    // paths below pay nothing when (as almost always) there is none.
    private bool _hasResultAdapter;
    private volatile bool _resultAdapterResolved;

    private static readonly bool HandlerIsDisposable =
        typeof(IDisposable).IsAssignableFrom(typeof(THandler)) || typeof(IAsyncDisposable).IsAssignableFrom(typeof(THandler));

    private readonly Func<THandler>? _directHandlerFactory = HandlerIsDisposable ? null : directHandlerFactory;
    private readonly Func<IServiceProvider, THandler>? _providerHandlerFactory = HandlerIsDisposable ? null : providerHandlerFactory;

    private IMessageDependencies? _cachedDependencies;
    private MessageDependencies? _cachedFastDependencies;
    private bool _useDirectConstruction;

    public ValueTask<TResult> Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider)
    {
        if (!_resultAdapterResolved)
        {
            _hasResultAdapter = global::Stella.Ergosfare.Core.Abstractions.Results
                .ResultAdapterBinding.For<TMessage, TResult>(serviceProvider) is not null;
            _resultAdapterResolved = true;
        }

        var dependencies = GetDependencies();

        if (_cachedFastDependencies?.FastSingleHandler is { } handlerReference
            && !_hasResultAdapter)
        {
            IHandler handler = _useDirectConstruction && handlerReference.HandlerType == typeof(THandler)
                ? _directHandlerFactory is not null ? _directHandlerFactory() : _providerHandlerFactory!(serviceProvider)
                : handlerReference.Resolve(serviceProvider);

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
                    case IAsyncHandler<TMessage, TResult> asyncHandler:
                        return AbortShortCircuit.Guard(asyncHandler.HandleAsync((TMessage)message, context));
                    case IHandler<TMessage, ValueTask<TResult>> valueTaskShaped:
                        return AbortShortCircuit.Guard(valueTaskShaped.Handle((TMessage)message, context));
                    case IHandler<TMessage, TResult> syncHandler:
                        return ValueTask.FromResult(syncHandler.Handle((TMessage)message, context));
                }
            }
            catch (ExecutionAbortedException)
            {
                return ValueTask.FromResult<TResult>(default!);
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
#pragma warning restore CS8714
