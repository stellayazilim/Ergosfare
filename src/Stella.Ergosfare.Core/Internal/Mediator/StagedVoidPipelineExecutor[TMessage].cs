using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Void pipeline hosting a staged plan: bespoke straight-line code for the message's whole
/// interceptor-bearing pipeline. The plan is advisory — the registry-version-guarded
/// dependency cache re-validates the plan's composition against the live pipeline
/// (<see cref="StagedPlanGate"/>) plus the memoization and adapter gates, and any mismatch
/// falls back to the runtime strategy, preserving semantics exactly. The plan resolves its
/// participants from the dispatching scope's provider — outside memoized mode that is
/// literally what the runtime handler references do, so container semantics are preserved.
/// Like every version-guarded executor, a dispatch racing a registration may run the
/// previous shape once; it never runs a shape the registry has not published.
/// </summary>
internal sealed class StagedVoidPipelineExecutor<TMessage>(
    IMessageDependenciesFactory dependenciesFactory,
    string[] groups,
    StagedVoidPlan<TMessage> plan) : IPipelineExecutor
    where TMessage : IMessage
{
    private readonly SingleAsyncHandlerMediationStrategy<TMessage> _strategy = new();

    // Whether the pipeline's Unit slot has an effective adapter — the attribute tiers
    // plus the container's default, resolved once on the first dispatch. Void plans
    // never model an adapter (a Unit carrier is a deliberate oddity), so any bound
    // adapter keeps the dispatch on the runtime strategy, which probes it.
    private bool _hasResultAdapter;
    private volatile bool _resultAdapterResolved;

    private IMessageDependencies? _cachedDependencies;
    private bool _useStagedPlan;
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
            if (_cachedDependencies is { } cached)
            {
                return cached;
            }

            var dependencies = typedFactory.Create(typeof(TMessage), groups);
            _cachedDependencies = dependencies;
            _useStagedPlan = !_hasResultAdapter
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
