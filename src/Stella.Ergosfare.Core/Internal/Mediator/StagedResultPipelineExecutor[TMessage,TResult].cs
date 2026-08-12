using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Result-producing counterpart of <see cref="StagedVoidPipelineExecutor{TMessage}"/>;
/// the same advisory contract and gates apply.
/// </summary>
internal sealed class StagedResultPipelineExecutor<TMessage, TResult>(
    IMessageDependenciesFactory dependenciesFactory,
    string[] groups,
    StagedResultPlan<TMessage, TResult> plan) : IPipelineExecutor<TResult>
    where TMessage : IMessage
{
    private readonly SingleAsyncHandlerMediationStrategy<TMessage, TResult> _strategy = new();

    // The identity of the result slot's effective adapter — the attribute tiers plus the
    // container's default, resolved once on the first dispatch (the default tier needs
    // the provider; the container is sealed after build, so the resolution never
    // changes). Part of the plan gate below: the plan is only trusted while its baked
    // adapter is exactly the one the runtime binds (both null in the overwhelmingly
    // common case), so a plan emitted without the slot's value-path branches never
    // serves an adapted pipeline.
    private Type? _effectiveResultAdapterType;
    private volatile bool _resultAdapterResolved;

    private IMessageDependencies? _cachedDependencies;
    private bool _useStagedPlan;
    private bool _useDirectConstruction;

    public ValueTask<TResult> Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider)
    {
        if (!_resultAdapterResolved)
        {
            _effectiveResultAdapterType = global::Stella.Ergosfare.Core.Abstractions.Results
                .ResultAdapterBinding.For<TMessage, TResult>(serviceProvider)?.GetType();
            _resultAdapterResolved = true;
        }

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
            // Frozen registry: dependencies resolve once per executor and are never
            // re-validated — a registration after the first dispatch is not observed.
            var cached = _cachedDependencies;

            if (cached is not null)
            {
                return cached;
            }

            var dependencies = typedFactory.Create(typeof(TMessage), groups);
            _cachedDependencies = dependencies;
            _useStagedPlan = plan.Composition.ResultAdapterType == _effectiveResultAdapterType
                && dependencies is MessageDependencies { MemoizedInstances: false } fastDependencies
                && StagedPlanGate.Matches(fastDependencies, plan.Composition);
            _useDirectConstruction = _useStagedPlan
                && plan.SupportsDirectConstruction
                && StagedPlanGate.AllPlainTransient(typedFactory, plan.Composition);
            return dependencies;
        }

        _useStagedPlan = false;
        _useDirectConstruction = false;
        return dependenciesFactory.Create(typeof(TMessage), groups);
    }
}
