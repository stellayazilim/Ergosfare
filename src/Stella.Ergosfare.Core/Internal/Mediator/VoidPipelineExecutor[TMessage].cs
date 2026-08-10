using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Void pipeline closed over the concrete <typeparamref name="TMessage"/>; see
/// <see cref="ResultPipelineExecutor{TMessage, TResult}"/>.
/// </summary>
internal sealed class VoidPipelineExecutor<TMessage>(
    IMessageDescriptor descriptor,
    IMessageDependenciesFactory dependenciesFactory,
    IResultAdapterService? resultAdapterService,
    string[] groups) : IPipelineExecutor
    where TMessage : IMessage
{
    private readonly SingleAsyncHandlerMediationStrategy<TMessage> _strategy = new(resultAdapterService);

    private readonly ResultAdapterService? _concreteAdapters = resultAdapterService as ResultAdapterService;
    private readonly bool _foreignAdapters = resultAdapterService is not null and not ResultAdapterService;

    private IMessageDependencies? _cachedDependencies;
    private MessageDependencies? _cachedFastDependencies;
    private int _cachedVersion = int.MinValue;

    public ValueTask Execute(object message, IExecutionContext context, IServiceProvider serviceProvider)
    {
        var dependencies = GetDependencies();

        if (_cachedFastDependencies?.FastSingleHandler is { } handlerReference
            && !_foreignAdapters
            && (_concreteAdapters is null || _concreteAdapters.IsEmpty))
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
            // Read before the build: a registration completing mid-build must land as a
            // version mismatch on the next dispatch, never as a fresh stamp on stale deps.
            var registryVersion = typedFactory.CurrentRegistryVersion;
            var cached = _cachedDependencies;

            if (cached is not null && _cachedVersion == registryVersion)
            {
                return cached;
            }

            var dependencies = typedFactory.Create(typeof(TMessage), descriptor, groups);
            _cachedFastDependencies = dependencies as MessageDependencies;
            _cachedDependencies = dependencies;
            _cachedVersion = registryVersion;
            return dependencies;
        }

        return dependenciesFactory.Create(typeof(TMessage), descriptor, groups);
    }
}
