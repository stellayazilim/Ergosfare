// The executor ends a stream message's channel when its pipeline stops, which is what the
// experimental streaming surface exists for.
#pragma warning disable ERGOEXP003

using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Results;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One container's result-producing pipeline for one (message, result) pair; the
/// counterpart of <see cref="FrozenVoidDispatch{TMessage}"/>, settling the same way on its
/// first dispatch.
/// </summary>
/// <typeparam name="TMessage">The message type this pipeline serves.</typeparam>
/// <typeparam name="TResult">The result type it produces.</typeparam>
/// <remarks>
/// Having a real result adds one more condition the void side does not have: the plan is
/// verified only while the adapter it was compiled against is exactly the one bound here —
/// both absent, in the common case — so a plan compiled without the branches an adapter
/// needs never serves an adapted pipeline, and the mismatch fails the dispatch naming both
/// types.
/// </remarks>
#pragma warning disable CS8714 // TResult is used as a pattern type argument; handler contracts declare notnull results
internal sealed class FrozenResultDispatch<TMessage, TResult> : IPipelineExecutor<TResult>
    where TMessage : IMessage
{

    private readonly IMessageDependenciesFactory _factory;
    private readonly StagedResultPlan<TMessage, TResult>? _plan;
    private readonly GroupedCompositions _grouped;

    // The adapter bound to this pipeline's result type, resolved on the first dispatch as in
    // the void pipeline. Which adapter it is decides whether the plan may run at all.
    private IResultAdapter<TResult>? _resultAdapter;
    private volatile bool _resultAdapterResolved;

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}._verdict"/>
    private volatile int _verdict;

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}.Undecided"/>
    // ReSharper disable once UnusedMember.Local
    private const int Undecided = 0;

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}.UsePlan"/>
    private const int UsePlan = 2;

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}.UsePlanDirect"/>
    private const int UsePlanDirect = 3;

    /// <summary>
    /// Initializes the pipeline over a container's factory and the plan compiled for this
    /// pair, if any.
    /// </summary>
    /// <param name="dependenciesFactory">The factory participants are verified through.</param>
    /// <param name="plan">
    /// The compiled plan for the unfiltered pipeline, or <c>null</c> for a pair whose plans
    /// are all per-set; see <see cref="FrozenVoidDispatch{TMessage}"/>.
    /// </param>
    public FrozenResultDispatch(IMessageDependenciesFactory dependenciesFactory, StagedResultPlan<TMessage, TResult>? plan)
    {
        _factory = dependenciesFactory;
        _plan = plan;
        _grouped = new GroupedCompositions(dependenciesFactory, typeof(TMessage), AdmitGroupedPlan);
    }

    /// <inheritdoc />
    public ValueTask<TResult> Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups)
    {
        // A message that carries chunks has a second half the pipeline has to close. One type
        // test on the ordinary path; the wrapper exists only where there is a stream.
        if (message is global::Stella.Ergosfare.Core.Abstractions.Streaming.ErgosfareStream stream)
        {
            return ExecuteAndEndStream(stream, message, context, serviceProvider, groups);
        }

        return ExecuteCore(message, context, serviceProvider, groups);
    }

    /// <summary>
    /// Runs the pipeline and ends the message's stream however it turns out.
    /// </summary>
    /// <param name="stream">The message's chunk-carrying half.</param>
    /// <param name="message">The message to run.</param>
    /// <param name="context">The execution context for this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved against.</param>
    /// <param name="groups">The groups to run.</param>
    /// <returns>The result the pipeline produced.</returns>
    /// <remarks>
    /// A pipeline that stops has to end the stream with it. The two are separate
    /// synchronisation objects, so a caller waiting on a full buffer learns nothing from the
    /// dispatch failing — it would wait for a reader that is never coming.
    /// </remarks>
    private async ValueTask<TResult> ExecuteAndEndStream(global::Stella.Ergosfare.Core.Abstractions.Streaming.ErgosfareStream stream, object message, ErgosfareContext context,
        IServiceProvider serviceProvider, IEnumerable<string>? groups)
    {
        try
        {
            return await ExecuteCore(message, context, serviceProvider, groups).ConfigureAwait(false);
        }
        finally
        {
            stream.EndDispatch();
        }
    }

    /// <summary>
    /// The pipeline itself, for a message with chunks or without.
    /// </summary>
    /// <param name="message">The message to run.</param>
    /// <param name="context">The execution context for this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved against.</param>
    /// <param name="groups">The groups to run.</param>
    /// <returns>The result the pipeline produced.</returns>
    private ValueTask<TResult> ExecuteCore(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups)
    {
        if (groups is not null)
        {
            return ExecuteGrouped(message, context, serviceProvider, groups);
        }

        var verdict = _verdict;

        if (verdict >= UsePlan)
        {
            // A settled verdict proves the plan: only ExecuteUndecided writes one, and it
            // has thrown by then for a pair without an unfiltered plan.
            return verdict == UsePlanDirect
                ? _plan!.ExecuteDirect((TMessage)message, context, serviceProvider)
                : _plan!.Execute((TMessage)message, context, serviceProvider);
        }

        return ExecuteUndecided(message, context, serviceProvider);
    }

    /// <summary>
    /// Runs the first dispatch and settles the plan variant the rest run; see
    /// <see cref="FrozenVoidDispatch{TMessage}.ExecuteUndecided"/>.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <returns>The result the pipeline produced.</returns>
    private ValueTask<TResult> ExecuteUndecided(object message, ErgosfareContext context, IServiceProvider serviceProvider)
    {
        if (_factory is not MessageDependenciesFactory typedFactory)
        {
            throw UnplannedDispatch.ForForeignFactory(typeof(TMessage));
        }

        EnsureResultAdapter(serviceProvider);

        // Throws for a message no composition serves — and on every dispatch of such a
        // message, since nothing is cached until the plan is verified.
        if (typedFactory.Create(typeof(TMessage), []) is not MessageDependencies dependencies)
        {
            throw UnplannedDispatch.ForForeignFactory(typeof(TMessage));
        }

        // Every plan this pair has is per-set, so the unfiltered dispatch has nothing to
        // run — a contested default set fails as the contest it is.
        if (_plan is null)
        {
            throw UnplannedDispatch.ForMissingPlan(typeof(TMessage), dependencies);
        }

        if (_plan.Composition.ResultAdapterType != _resultAdapter?.GetType())
        {
            throw UnplannedDispatch.ForResultAdapterMismatch(
                typeof(TMessage), _plan.Composition.ResultAdapterType, _resultAdapter?.GetType());
        }

        if (dependencies.ForcedMemoization)
        {
            throw UnplannedDispatch.ForMemoizedInstances(typeof(TMessage));
        }

        if (!StagedPlanGate.Matches(dependencies, _plan.Composition))
        {
            throw UnplannedDispatch.ForDivergedComposition(typeof(TMessage), dependencies, _plan.Composition);
        }

        _verdict = _plan.SupportsDirectConstruction
                   && StagedPlanGate.AllPlainTransient(typedFactory, _plan.Composition)
            ? UsePlanDirect
            : UsePlan;

        return _verdict == UsePlanDirect
            ? _plan.ExecuteDirect((TMessage)message, context, serviceProvider)
            : _plan.Execute((TMessage)message, context, serviceProvider);
    }

    /// <summary>
    /// Runs a dispatch that named groups; see
    /// <see cref="FrozenVoidDispatch{TMessage}.ExecuteGrouped"/>.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <param name="groups">The groups the dispatch asked for.</param>
    /// <returns>The result the pipeline produced.</returns>
    private ValueTask<TResult> ExecuteGrouped(object message, ErgosfareContext context,
        IServiceProvider serviceProvider, IEnumerable<string> groups)
    {
        EnsureResultAdapter(serviceProvider);

        var composition = _grouped.Resolve(groups);
        var groupedPlan = (StagedResultPlan<TMessage, TResult>)composition.Admission.Plan!;

        // A plan compiled for this exact set already knows its participants; the filtering
        // plan works them out from the set it is handed.
        if (groupedPlan.FilterGroups is not null)
        {
            return composition.Admission.Direct
                ? groupedPlan.ExecuteFilteredDirect((TMessage)message, context, serviceProvider, composition.Groups)
                : groupedPlan.ExecuteFiltered((TMessage)message, context, serviceProvider, composition.Groups);
        }

        return composition.Admission.Direct
            ? groupedPlan.ExecuteDirect((TMessage)message, context, serviceProvider)
            : groupedPlan.Execute((TMessage)message, context, serviceProvider);
    }

    /// <summary>
    /// Decides which compiled plan serves one group set, or fails the dispatch when none
    /// verifiably does; see <see cref="FrozenVoidDispatch{TMessage}.AdmitGroupedPlan"/>.
    /// </summary>
    /// <param name="groups">The group set being decided for.</param>
    /// <param name="dependencies">The participants that set selects.</param>
    /// <returns>The plan and whether it may construct participants itself.</returns>
    /// <remarks>
    /// The adapter must match here too, exactly as it must for the ungrouped route.
    /// </remarks>
    private GroupedPlanAdmission AdmitGroupedPlan(string[] groups, IMessageDependencies dependencies)
    {
        var plan = GeneratedDispatchRoots.FindStagedResultPlan(typeof(TMessage), typeof(TResult), groups)
            as StagedResultPlan<TMessage, TResult>;

        if (plan is not null)
        {
            VerifyAdapter(plan.Composition);
            VerifyComposition(dependencies, plan.Composition);
        }
        else
        {
            // No plan was compiled for this set; the void dispatch explains what the
            // filtering plan has to be checked against.
            plan = GeneratedDispatchRoots.FindFilteredResultPlan(typeof(TMessage), typeof(TResult))
                as StagedResultPlan<TMessage, TResult>;

            if (plan?.FilterGroups is not { } covered)
            {
                throw UnplannedDispatch.ForUnplannedGroupSet(typeof(TMessage), groups);
            }

            VerifyAdapter(plan.Composition);

            if (_factory.Find(typeof(TMessage), covered) is not { } full)
            {
                throw UnplannedDispatch.ForUnplannedGroupSet(typeof(TMessage), groups);
            }

            VerifyComposition(full, plan.Composition);
        }

        var direct = plan.SupportsDirectConstruction
                     && _factory is MessageDependenciesFactory typedFactory
                     && StagedPlanGate.AllPlainTransient(typedFactory, plan.Composition);

        return new GroupedPlanAdmission(plan, direct);
    }

    /// <summary>
    /// Verifies that the adapter bound here is the one <paramref name="composition"/> was
    /// compiled against.
    /// </summary>
    /// <param name="composition">The pipeline the plan was compiled against.</param>
    private void VerifyAdapter(StagedPlanKey composition)
    {
        if (composition.ResultAdapterType != _resultAdapter?.GetType())
        {
            throw UnplannedDispatch.ForResultAdapterMismatch(
                typeof(TMessage), composition.ResultAdapterType, _resultAdapter?.GetType());
        }
    }

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}.VerifyComposition"/>
    private static void VerifyComposition(IMessageDependencies dependencies, StagedPlanKey composition)
    {
        if (dependencies is not MessageDependencies fast)
        {
            throw UnplannedDispatch.ForForeignFactory(typeof(TMessage));
        }

        if (fast.ForcedMemoization)
        {
            throw UnplannedDispatch.ForMemoizedInstances(typeof(TMessage));
        }

        if (!StagedPlanGate.Matches(fast, composition))
        {
            throw UnplannedDispatch.ForDivergedComposition(typeof(TMessage), fast, composition);
        }
    }

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}.EnsureResultAdapter"/>
    private void EnsureResultAdapter(IServiceProvider serviceProvider)
    {
        if (_resultAdapterResolved)
        {
            return;
        }

        _resultAdapter = ResultAdapterBinding.For<TMessage, TResult>(serviceProvider);
        _resultAdapterResolved = true;
    }
}
#pragma warning restore CS8714
