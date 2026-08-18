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
/// One container's void pipeline for one message type: it runs the compiled plan, and
/// nothing else. A dispatch the plan cannot serve fails, precisely.
/// </summary>
/// <typeparam name="TMessage">The message type this pipeline serves.</typeparam>
/// <remarks>
/// Everything is settled on the first dispatch — the participants are resolved once and the
/// plan is verified against them — after which every dispatch reads one field and runs the
/// plan. A pipeline the plan was not compiled against does not degrade into a runtime lane;
/// it raises <see cref="Abstractions.Exceptions.UnplannedDispatchException"/> naming what
/// diverged, on every dispatch, since nothing is cached when the verification fails. A
/// group-filtered dispatch picks its own plan through a per-set slot the same way.
/// </remarks>
internal sealed class FrozenVoidDispatch<TMessage> : IPipelineExecutor
    where TMessage : IMessage
{

    private readonly IMessageDependenciesFactory _factory;
    private readonly StagedVoidPlan<TMessage>? _plan;
    private readonly GroupedCompositions _grouped;

    // The adapter bound to this pipeline's Unit slot. Resolved on the first dispatch because
    // the container's default tier needs a provider. Void plans never assume an adapter, so
    // one being bound fails the dispatch rather than silently skipping the adapter.
    private IResultAdapter<Unit>? _resultAdapter;
    private volatile bool _resultAdapterResolved;

    /// <summary>
    /// Which plan variant an ungrouped dispatch runs, decided on the first one and never
    /// revisited.
    /// </summary>
    /// <remarks>
    /// Volatile because writing it publishes everything decided alongside it: a reader that
    /// sees a settled verdict must also see the resolved adapter, which is written first.
    /// </remarks>
    private volatile int _verdict;

    /// <summary>
    /// Nothing has been decided yet — the value a new instance starts at, which is why it is
    /// never compared against.
    /// </summary>
    // ReSharper disable once UnusedMember.Local
    private const int Undecided = 0;

    /// <summary>
    /// Run the compiled plan.
    /// </summary>
    private const int UsePlan = 2;

    /// <summary>
    /// Run the compiled plan and let it construct participants itself.
    /// </summary>
    private const int UsePlanDirect = 3;

    /// <summary>
    /// Initializes the pipeline over a container's factory and the plan compiled for this
    /// message, if any.
    /// </summary>
    /// <param name="dependenciesFactory">The factory participants are verified through.</param>
    /// <param name="plan">
    /// The compiled plan for the unfiltered pipeline, or <c>null</c> for a message whose
    /// plans are all per-set — a contested default set that only groups can resolve. An
    /// ungrouped dispatch then fails precisely, while grouped ones run their own plans.
    /// </param>
    public FrozenVoidDispatch(IMessageDependenciesFactory dependenciesFactory, StagedVoidPlan<TMessage>? plan)
    {
        _factory = dependenciesFactory;
        _plan = plan;
        _grouped = new GroupedCompositions(dependenciesFactory, typeof(TMessage), AdmitGroupedPlan);
    }

    /// <inheritdoc />
    public ValueTask Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider,
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
    /// <returns>A task that completes when the pipeline has run.</returns>
    /// <remarks>
    /// A pipeline that stops has to end the stream with it. The two are separate
    /// synchronisation objects, so a caller waiting on a full buffer learns nothing from the
    /// dispatch failing — it would wait for a reader that is never coming.
    /// </remarks>
    private async ValueTask ExecuteAndEndStream(global::Stella.Ergosfare.Core.Abstractions.Streaming.ErgosfareStream stream, object message, ErgosfareContext context,
        IServiceProvider serviceProvider, IEnumerable<string>? groups)
    {
        try
        {
            await ExecuteCore(message, context, serviceProvider, groups).ConfigureAwait(false);
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
    /// <returns>A task that completes when the pipeline has run.</returns>
    private ValueTask ExecuteCore(object message, ErgosfareContext context, IServiceProvider serviceProvider,
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
            // has thrown by then for a message without an unfiltered plan.
            return verdict == UsePlanDirect
                ? _plan!.ExecuteDirect((TMessage)message, context, serviceProvider)
                : _plan!.Execute((TMessage)message, context, serviceProvider);
        }

        return ExecuteUndecided(message, context, serviceProvider);
    }

    /// <summary>
    /// Runs the first dispatch: binds the adapter, resolves the participants, verifies the
    /// plan against them, then runs it — or fails the dispatch naming what could not be
    /// verified.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    private ValueTask ExecuteUndecided(object message, ErgosfareContext context, IServiceProvider serviceProvider)
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

        // Every plan this message has is per-set, so the unfiltered dispatch has nothing to
        // run — a contested default set fails as the contest it is.
        if (_plan is null)
        {
            throw UnplannedDispatch.ForMissingPlan(typeof(TMessage), dependencies);
        }

        // Void plans never assume an adapter, so one being bound means branches the plan
        // does not have.
        if (_resultAdapter is not null)
        {
            throw UnplannedDispatch.ForResultAdapterMismatch(
                typeof(TMessage), compiledAdapterType: null, _resultAdapter.GetType());
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
    /// Runs a dispatch that named groups, through the plan those groups admit.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <param name="groups">The groups the dispatch asked for.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    private ValueTask ExecuteGrouped(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string> groups)
    {
        EnsureResultAdapter(serviceProvider);

        var composition = _grouped.Resolve(groups);
        var groupedPlan = (StagedVoidPlan<TMessage>)composition.Admission.Plan!;

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
    /// verifiably does.
    /// </summary>
    /// <param name="groups">The group set being decided for.</param>
    /// <param name="dependencies">The participants that set selects.</param>
    /// <returns>The plan and whether it may construct participants itself.</returns>
    /// <remarks>
    /// The same question <see cref="ExecuteUndecided"/> answers for the default set, asked
    /// once per set as its entry is built rather than on every dispatch. A set that fails
    /// here fails on every dispatch, since a failed decision is never slotted.
    /// </remarks>
    private GroupedPlanAdmission AdmitGroupedPlan(string[] groups, IMessageDependencies dependencies)
    {
        if (_resultAdapter is not null)
        {
            throw UnplannedDispatch.ForResultAdapterMismatch(
                typeof(TMessage), compiledAdapterType: null, _resultAdapter.GetType());
        }

        var plan = GeneratedDispatchRoots.FindStagedVoidPlan(typeof(TMessage), groups) as StagedVoidPlan<TMessage>;

        if (plan is not null)
        {
            VerifyComposition(dependencies, plan.Composition);
        }
        else
        {
            // No plan was compiled for this set. The filtering plan can serve any set, but
            // only if it is checked against the participants of the groups it covers —
            // the one set that reproduces everything its body holds.
            plan = GeneratedDispatchRoots.FindFilteredVoidPlan(typeof(TMessage)) as StagedVoidPlan<TMessage>;

            if (plan?.FilterGroups is not { } covered)
            {
                throw UnplannedDispatch.ForUnplannedGroupSet(typeof(TMessage), groups);
            }

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
    /// Verifies one live composition against what a plan compiled, failing the dispatch
    /// with the precise reason when they cannot be reconciled.
    /// </summary>
    /// <param name="dependencies">The live participants.</param>
    /// <param name="composition">The pipeline the plan was compiled against.</param>
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

    /// <summary>
    /// Binds this pipeline's result adapter, once.
    /// </summary>
    /// <param name="serviceProvider">The provider the container's default adapter comes from.</param>
    /// <remarks>
    /// The container is sealed once built, so the binding can never change afterwards. Two
    /// threads racing here both publish the same thing, and the volatile flag orders the
    /// publication.
    /// </remarks>
    private void EnsureResultAdapter(IServiceProvider serviceProvider)
    {
        if (_resultAdapterResolved)
        {
            return;
        }

        _resultAdapter = ResultAdapterBinding.For<TMessage, Unit>(serviceProvider);
        _resultAdapterResolved = true;
    }
}
