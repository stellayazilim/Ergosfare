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
    StagedResultPlan<TMessage, TResult> plan) : IPipelineExecutor<TResult>
    where TMessage : IMessage
{
    private static readonly string[] EmptyGroups = [];

    private readonly SingleAsyncHandlerMediationStrategy<TMessage, TResult> _strategy = new();

    /// <summary>
    /// The plain pipeline this executor becomes under a group filter; see
    /// <see cref="StagedVoidPipelineExecutor{TMessage}"/>.
    /// </summary>
    private readonly ResultPipelineExecutor<TMessage, TResult> _filtered = new(dependenciesFactory);

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

    /// <summary>
    /// The gate's verdict as one field; see <see cref="StagedVoidPipelineExecutor{TMessage}"/>
    /// for why the two facts share one.
    /// </summary>
    private int _verdict;

    private const int UseStrategy = 1;
    private const int UsePlan = 2;
    private const int UsePlanDirect = 3;

    public ValueTask<TResult> Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups)
    {
        if (groups is not null)
        {
            return _filtered.Execute(message, context, serviceProvider, groups);
        }

        // The steady state of a planned pipeline: one field read, then the plan; see
        // StagedVoidPipelineExecutor for why the composition is not consulted here.
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
    /// The first dispatch, and every dispatch of a pipeline whose gate said no.
    /// </summary>
    private ValueTask<TResult> ExecuteUngated(object message, ErgosfareContext context, IServiceProvider serviceProvider)
    {
        if (!_resultAdapterResolved)
        {
            _effectiveResultAdapterType = global::Stella.Ergosfare.Core.Abstractions.Results
                .ResultAdapterBinding.For<TMessage, TResult>(serviceProvider)?.GetType();
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

            var useStagedPlan = plan.Composition.ResultAdapterType == _effectiveResultAdapterType
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

        // A foreign factory answers per dispatch; see StagedVoidPipelineExecutor.
        _verdict = UseStrategy;
        return dependenciesFactory.Create(typeof(TMessage), EmptyGroups);
    }
}
