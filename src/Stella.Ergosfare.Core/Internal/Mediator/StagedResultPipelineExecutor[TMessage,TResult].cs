using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
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
    private bool _useDirectConstruction;

    public ValueTask<TResult> Execute(object message, IExecutionContext context, IServiceProvider serviceProvider)
    {
        var dependencies = GetDependencies();

        if (_useStagedPlan)
        {
            return _useDirectConstruction
                ? plan.ExecuteDirect((TMessage)message, context, serviceProvider)
                : plan.Execute((TMessage)message, context, serviceProvider);
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
            _cachedDependencies = dependencies;
            _useStagedPlan = !_foreignAdapters
                && (_concreteAdapters is null || _concreteAdapters.IsEmpty)
                && dependencies is MessageDependencies { MemoizedInstances: false } fastDependencies
                && StagedPlanGate.Matches(fastDependencies, plan.Composition);
            _useDirectConstruction = _useStagedPlan
                && plan.SupportsDirectConstruction
                && StagedPlanGate.AllPlainTransient(typedFactory, plan.Composition);
            _cachedVersion = registryVersion;
            return dependencies;
        }

        _useStagedPlan = false;
        _useDirectConstruction = false;
        return dependenciesFactory.Create(typeof(TMessage), descriptor, groups);
    }
}
