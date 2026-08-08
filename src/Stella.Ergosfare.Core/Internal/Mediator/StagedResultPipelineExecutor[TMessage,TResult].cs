using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Result-producing counterpart of <see cref="StagedVoidPipelineExecutor{TMessage}"/>;
/// the same advisory contract and gates apply.
/// </summary>
internal sealed class StagedResultPipelineExecutor<TMessage, TResult>(
    IMessageDescriptor descriptor,
    IMessageDependenciesFactory dependenciesFactory,
    IResultAdapterService? resultAdapterService,
    string[] groups,
    StagedResultPlan<TMessage, TResult> plan) : IPipelineExecutor<TResult>
    where TMessage : notnull, IMessage
{
    private readonly SingleAsyncHandlerMediationStrategy<TMessage, TResult> _strategy = new(resultAdapterService);

    private readonly ResultAdapterService? _concreteAdapters = resultAdapterService as ResultAdapterService;
    private readonly bool _foreignAdapters = resultAdapterService is not null and not ResultAdapterService;

    private IMessageDependencies? _cachedDependencies;
    private int _cachedVersion = int.MinValue;
    private bool _useStagedPlan;

    public ValueTask<TResult> Execute(object message, IExecutionContext context, IServiceProvider serviceProvider)
    {
        var dependencies = GetDependencies();

        if (_useStagedPlan)
        {
            return plan.Execute((TMessage)message, context, serviceProvider);
        }

        return _strategy.Mediate((TMessage)message, dependencies, context, serviceProvider);
    }

    private IMessageDependencies GetDependencies()
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            var cached = _cachedDependencies;

            if (cached is not null && _cachedVersion == typedFactory.CurrentRegistryVersion)
            {
                return cached;
            }

            var dependencies = typedFactory.Create(typeof(TMessage), descriptor, groups);
            _cachedDependencies = dependencies;
            _useStagedPlan = !_foreignAdapters
                && (_concreteAdapters is null || _concreteAdapters.IsEmpty)
                && dependencies is MessageDependencies { MemoizedInstances: false } fastDependencies
                && StagedPlanGate.Matches(fastDependencies, plan.Composition);
            _cachedVersion = typedFactory.CurrentRegistryVersion;
            return dependencies;
        }

        _useStagedPlan = false;
        return dependenciesFactory.Create(typeof(TMessage), descriptor, groups);
    }
}
