using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Result-producing pipeline closed over the concrete <typeparamref name="TMessage"/>.
/// Because <typeparamref name="TMessage"/> is the message's runtime type here, the mediation
/// strategy's typed seam always hits — the handler is invoked through its typed member and
/// its <see cref="ValueTask{TResult}"/> never crosses an object-typed bridge.
/// </summary>
#pragma warning disable CS8714 // TResult is used as a pattern type argument; handler contracts declare notnull results
internal sealed class ResultPipelineExecutor<TMessage, TResult>(
    IMessageDependenciesFactory dependenciesFactory,
    string[] groups) : IPipelineExecutor<TResult>
    where TMessage : notnull
{
    private readonly SingleAsyncHandlerMediationStrategy<TMessage, TResult> _strategy = new();

    // Whether the pipeline's result slot has an effective adapter — the attribute tiers
    // plus the container's default, resolved once on the first dispatch, so the fast
    // paths below pay nothing when (as almost always) there is none. Any adapter routes
    // the dispatch through the strategy, which owns the value channel.
    private bool _hasResultAdapter;
    private volatile bool _resultAdapterResolved;

    // Dependencies cached per executor (executors are already per message type + groups),
    // re-validated against the registry version — turns the per-dispatch factory call and
    // cache lookup into a single field read + version compare.
    private IMessageDependencies? _cachedDependencies;
    private MessageDependencies? _cachedFastDependencies;

    public ValueTask<TResult> Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider)
    {
        if (!_resultAdapterResolved)
        {
            _hasResultAdapter = global::Stella.Ergosfare.Core.Abstractions.Results
                .ResultAdapterBinding.For<TMessage, TResult>(serviceProvider) is not null;
            _resultAdapterResolved = true;
        }

        var dependencies = GetDependencies();

        // Zero-interceptor, single-handler, no-adapter dispatch: invoke the handler's typed
        // member directly and hand its ValueTask straight back — no async state machine,
        // no interface-dispatched Count checks. Mirrors the strategy's fast path exactly.
        if (_cachedFastDependencies?.FastSingleHandler is { } handlerReference
            && !_hasResultAdapter)
        {
            var handler = handlerReference.Resolve(serviceProvider);

            // The strategy is skipped here, so its abort handling has to be too; see
            // AbortShortCircuit. Nothing was produced when a zero-interceptor handler
            // aborts, so the caller gets the result type's default.
            try
            {
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

            // Unsupported handler contract: fall through so the strategy raises its
            // canonical NotSupportedException.
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

            // Races are benign: both writers publish equivalent, idempotent state.
            var dependencies = typedFactory.Create(typeof(TMessage), groups);
            _cachedFastDependencies = dependencies as MessageDependencies;
            _cachedDependencies = dependencies;
            return dependencies;
        }

        // Foreign factory implementations keep the original per-dispatch behavior.
        return dependenciesFactory.Create(typeof(TMessage), groups);
    }
}

#pragma warning restore CS8714
