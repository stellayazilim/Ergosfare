using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Void pipeline hosting a staged plan: bespoke straight-line code for the message's whole
/// interceptor-bearing pipeline. The plan is advisory — the dependency cache validates the
/// plan's composition against the live pipeline on the first dispatch
/// (<see cref="StagedPlanGate"/>) plus the memoization and adapter gates, and any mismatch
/// falls back to the runtime strategy, preserving semantics exactly. The plan resolves its
/// participants from the dispatching scope's provider — outside memoized mode that is
/// literally what the runtime handler references do, so container semantics are preserved.
/// The validated composition is frozen; a registration after the first dispatch is not
/// observed.
/// </summary>
internal sealed class StagedVoidPipelineExecutor<TMessage>(
    IMessageDependenciesFactory dependenciesFactory,
    StagedVoidPlan<TMessage> plan) : IPipelineExecutor
    where TMessage : IMessage
{
    private static readonly string[] EmptyGroups = [];

    private readonly SingleAsyncHandlerMediationStrategy<TMessage> _strategy = new();

    /// <summary>
    /// The plain pipeline this executor becomes under a group filter. The plan was baked
    /// against the unfiltered composition, so a filter — which may exclude any planned
    /// participant — has no plan to take and wants exactly the runtime shape.
    /// </summary>
    private readonly VoidPipelineExecutor<TMessage> _filtered = new(dependenciesFactory);

    // Whether the pipeline's Unit slot has an effective adapter — the attribute tiers
    // plus the container's default, resolved once on the first dispatch. Void plans
    // never model an adapter (a Unit carrier is a deliberate oddity), so any bound
    // adapter keeps the dispatch on the runtime strategy, which probes it.
    private bool _hasResultAdapter;
    private volatile bool _resultAdapterResolved;

    private IMessageDependencies? _cachedDependencies;

    /// <summary>
    /// The gate's verdict — whether the plan serves this pipeline, and if so whether its
    /// direct-construction variant qualifies — as one field.
    /// </summary>
    /// <remarks>
    /// One <c>int</c> rather than two <c>bool</c>s because the two facts have to be seen
    /// together: a reader that took the plan arm on a stale companion flag would construct
    /// participants the container was supposed to resolve. A single field cannot be
    /// observed half-decided, so the pair needs neither a volatile read on the dispatch
    /// path nor a reference to chase. Races are benign — every writer computes the same
    /// verdict from a frozen composition.
    /// </remarks>
    private int _verdict;

    private const int Undecided = 0;
    private const int UseStrategy = 1;
    private const int UsePlan = 2;
    private const int UsePlanDirect = 3;

    public ValueTask Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups)
    {
        if (groups is not null)
        {
            return _filtered.Execute(message, context, serviceProvider, groups);
        }

        // The steady state of a planned pipeline: one field read, then the plan. The gate
        // asks a question about the composition, and the composition is frozen by the first
        // dispatch — so every dispatch after it has nothing to decide and nothing to
        // resolve. This used to call GetDependencies() first and discard the answer on this
        // arm, which cost an interface type test and two field reads per dispatch to
        // compute something the plan does not use.
        var verdict = _verdict;

        if (verdict >= UsePlan)
        {
            return verdict == UsePlanDirect
                ? plan.ExecuteDirect((TMessage)message, context, serviceProvider)
                : plan.Execute((TMessage)message, context, serviceProvider);
        }

        return ExecuteUngated(message, context, serviceProvider);
    }

    /// <summary>
    /// The first dispatch, and every dispatch of a pipeline whose gate said no: resolve the
    /// composition (which decides the gate), then take whichever arm it chose.
    /// </summary>
    private ValueTask ExecuteUngated(object message, ErgosfareContext context, IServiceProvider serviceProvider)
    {
        if (!_resultAdapterResolved)
        {
            _hasResultAdapter = global::Stella.Ergosfare.Core.Abstractions.Results
                .ResultAdapterBinding.For<TMessage, Unit>(serviceProvider) is not null;
            _resultAdapterResolved = true;
        }

        var dependencies = GetDependencies();
        var verdict = _verdict;

        if (verdict >= UsePlan)
        {
            return verdict == UsePlanDirect
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

            var dependencies = typedFactory.Create(typeof(TMessage), EmptyGroups);
            _cachedDependencies = dependencies;

            var useStagedPlan = !_hasResultAdapter
                && dependencies is MessageDependencies { MemoizedInstances: false } fastDependencies
                && StagedPlanGate.Matches(fastDependencies, plan.Composition);

            _verdict = useStagedPlan
                ? plan.SupportsDirectConstruction
                  && StagedPlanGate.AllPlainTransient(typedFactory, plan.Composition)
                    ? UsePlanDirect
                    : UsePlan
                : UseStrategy;

            return dependencies;
        }

        // A foreign factory answers per dispatch, so the verdict stays at UseStrategy and
        // this executor never leaves the ungated path.
        _verdict = UseStrategy;
        return dependenciesFactory.Create(typeof(TMessage), EmptyGroups);
    }
}
