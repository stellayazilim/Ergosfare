using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Void pipeline closed over the concrete <typeparamref name="TMessage"/>; see
/// <see cref="ResultPipelineExecutor{TMessage, TResult}"/>.
/// </summary>
internal sealed class VoidPipelineExecutor<TMessage>(
    IMessageDependenciesFactory dependenciesFactory,
    string[] groups) : IPipelineExecutor
    where TMessage : IMessage
{
    private readonly SingleAsyncHandlerMediationStrategy<TMessage> _strategy = new();

    // Whether the pipeline's Unit slot has an effective adapter — the attribute tiers
    // plus the container's default, resolved once on the first dispatch, so the fast
    // paths below pay nothing when (as almost always) there is none.
    private bool _hasResultAdapter;
    private volatile bool _resultAdapterResolved;

    private IMessageDependencies? _cachedDependencies;
    private MessageDependencies? _cachedFastDependencies;

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
            var handler = handlerReference.Resolve(serviceProvider);

            // The strategy is skipped here, so its abort handling has to be too; see
            // AbortShortCircuit. The try covers a handler that aborts synchronously, the
            // guard the one that captured the abort into its task.
            try
            {
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
            _cachedFastDependencies = dependencies as MessageDependencies;
            _cachedDependencies = dependencies;
            return dependencies;
        }

        return dependenciesFactory.Create(typeof(TMessage), groups);
    }
}
