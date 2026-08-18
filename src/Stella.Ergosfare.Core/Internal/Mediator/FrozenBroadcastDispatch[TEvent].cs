using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// <see cref="FrozenBroadcastDispatch"/> closed over one event type.
/// </summary>
/// <typeparam name="TEvent">The event type this dispatch publishes.</typeparam>
/// <remarks>
/// Everything is decided in the constructor: the ungrouped participants are resolved once
/// and the compiled plan is verified against them once, and what comes out is a mode that
/// never changes. Deciding this early is safe because the composition is settled before the
/// container is built, so the constructor sees exactly what the first publish would. A
/// publish the plan cannot serve fails on every publish with the reason settled here; an
/// event nobody serves still publishes to nobody, which is not an error.
/// </remarks>
internal sealed class FrozenBroadcastDispatch<TEvent> : FrozenBroadcastDispatch
    where TEvent : notnull
{
    // One copy per closed message type is deliberate — the dispatch itself is per type.
    // ReSharper disable once StaticMemberInGenericType

    /// <summary>
    /// The plan compiled for this event type, or <c>null</c> when the generator produced
    /// none.
    /// </summary>
    // ReSharper disable once StaticMemberInGenericType
    private static readonly StagedBroadcastPlan<TEvent>? Plan =
        GeneratedDispatchRoots.FindBroadcastPlan(typeof(TEvent)) as StagedBroadcastPlan<TEvent>;

    /// <summary>
    /// No composition serves this event type, so a publish reaches nobody.
    /// </summary>
    private const int NoPipelineMode = 0;

    /// <summary>
    /// The pipeline cannot be verified against a compiled plan; every publish fails with
    /// the settled reason.
    /// </summary>
    private const int UnplannedMode = 1;

    /// <summary>
    /// Run the compiled plan.
    /// </summary>
    private const int PlanMode = 3;

    /// <summary>
    /// Run the compiled plan and let it construct participants itself.
    /// </summary>
    private const int PlanDirectMode = 4;

    private readonly IMessageDependenciesFactory _factory;

    /// <summary>
    /// How an ungrouped publish is delivered, decided once in the constructor.
    /// </summary>
    private readonly int _mode;

    /// <summary>
    /// Builds the failure an <see cref="UnplannedMode"/> publish raises; <c>null</c> in
    /// every other mode.
    /// </summary>
    /// <remarks>
    /// A factory rather than one instance, so every publish throws a fresh exception with
    /// its own stack.
    /// </remarks>
    private readonly Func<Exception>? _unplanned;

    private GroupedSlot? _cachedGroupedSlot;

    /// <summary>
    /// Resolves this event's participants and settles how it will be published.
    /// </summary>
    /// <param name="dependenciesFactory">The factory participants are verified through.</param>
    public FrozenBroadcastDispatch(IMessageDependenciesFactory dependenciesFactory)
    {
        _factory = dependenciesFactory;

        var dependencies = dependenciesFactory.Find(typeof(TEvent), []);

        if (dependencies is null)
        {
            // No composition serves this event type. Publishing it reaches nobody, which is
            // not an error at run time.
            _mode = NoPipelineMode;
            return;
        }

        if (dependencies is not MessageDependencies fast)
        {
            _mode = UnplannedMode;
            _unplanned = static () => UnplannedDispatch.ForForeignFactory(typeof(TEvent));
            return;
        }

        if (fast.HandlerArray.Length == 0 && fast.IndirectHandlerArray.Length == 0)
        {
            // The default set selects no handler — a group-only event published without
            // groups, or a composition carrying nothing but interceptor rows. Reaching
            // nobody is not an error, and it needs no plan to happen; checked before the
            // plan is verified because an empty shape proves nothing about one.
            _mode = NoPipelineMode;
            return;
        }

        if (Plan is null)
        {
            _mode = UnplannedMode;
            _unplanned = static () => UnplannedDispatch.ForMissingBroadcastPlan(typeof(TEvent));
            return;
        }

        if (fast.ForcedMemoization)
        {
            _mode = UnplannedMode;
            _unplanned = static () => UnplannedDispatch.ForMemoizedInstances(typeof(TEvent));
            return;
        }

        if (!StagedPlanGate.Matches(fast, Plan.Composition))
        {
            _mode = UnplannedMode;
            _unplanned = () => UnplannedDispatch.ForDivergedBroadcastComposition(
                typeof(TEvent), fast, Plan.Composition);
            return;
        }

        _mode = Plan.SupportsDirectConstruction
                && dependenciesFactory is MessageDependenciesFactory typedFactory
                && StagedPlanGate.AllPlainTransient(typedFactory, Plan.Composition)
            ? PlanDirectMode
            : PlanMode;
    }

    /// <inheritdoc />
    internal override ValueTask Publish(
        object message,
        ErgosfareContext context,
        IServiceProvider serviceProvider,
        IEnumerable<string>? groups)
        => PublishCore(message, context, serviceProvider, groups);

    /// <inheritdoc />
    internal override ValueTask PublishPooled(
        object message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        IEnumerable<string>? groups)
    {
        var context = ErgosfareContextPool.Rent(null, cancellationToken);
        ValueTask task;

        try
        {
            task = PublishCore(message, context, serviceProvider, groups);
        }
        catch
        {
            ErgosfareContextPool.Return(context);
            throw;
        }

        // A publish that finished synchronously — the common case — releases its context
        // here, so no async state machine is built for it. Only one that actually suspended
        // pays for the helper below.
        if (task.IsCompletedSuccessfully)
        {
            ErgosfareContextPool.Return(context);
            return default;
        }

        return AwaitAndReturn(task, context);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask AwaitAndReturn(ValueTask task, ErgosfareContext context)
        {
            try
            {
                await task;
            }
            finally
            {
                ErgosfareContextPool.Return(context);
            }
        }
    }

    /// <summary>
    /// Delivers a publish through whichever plan its mode names, or fails it.
    /// </summary>
    /// <param name="message">The event to publish.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <param name="serviceProvider">The provider handlers are resolved from.</param>
    /// <param name="groups">The groups to deliver to, or <c>null</c> for the default.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask PublishCore(
        object message,
        ErgosfareContext context,
        IServiceProvider serviceProvider,
        IEnumerable<string>? groups)
    {
        if (groups is not (null or List<string> { Count: 0 } or string[] { Length: 0 } or GroupSet { Count: 0 }))
        {
            return PublishGrouped(message, context, serviceProvider, groups);
        }

        var mode = _mode;

        if (mode >= PlanMode)
        {
            return mode == PlanDirectMode
                ? Plan!.ExecuteDirect((TEvent)message, context, serviceProvider)
                : Plan!.Execute((TEvent)message, context, serviceProvider);
        }

        if (mode == UnplannedMode)
        {
            throw _unplanned!();
        }

        return NoPipeline();
    }

    /// <summary>
    /// Delivers a publish that named groups, through the plan those groups admit.
    /// </summary>
    /// <param name="message">The event to publish.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <param name="serviceProvider">The provider handlers are resolved from.</param>
    /// <param name="groups">The groups the publish asked for.</param>
    /// <returns>A task that completes when every matching handler has run.</returns>
    /// <remarks>
    /// A single last-used slot serves the common shape of one stable group set per event
    /// type; a miss rebuilds through the factory's own per-(type, set) cache, so alternating
    /// sets stay cheap and each set gets its own settled decision. A set that fails
    /// admission fails on every publish, since a failed decision is never slotted.
    /// </remarks>
    private ValueTask PublishGrouped(
        object message,
        ErgosfareContext context,
        IServiceProvider serviceProvider,
        IEnumerable<string> groups)
    {
        var slot = _cachedGroupedSlot;

        // Deliberate: groups is compared without being copied first, and copied only on a
        // miss.
        // ReSharper disable once PossibleMultipleEnumeration
        if (slot is null || !GroupSlotMatch.Matches(groups, slot.Groups, slot.Canonical))
        {
            // A canonical set hands over its own immutable array, so it costs no copy.
            var canonical = groups as GroupSet;
            // ReSharper disable once PossibleMultipleEnumeration
            var materialized = canonical?.Names ?? [.. groups];
            var dependencies = _factory.Find(typeof(TEvent), materialized);

            if (dependencies is null)
            {
                // Not slotted: the catalog caches its own misses per type, so asking again
                // costs a dictionary lookup — and an empty slot could not be told apart from
                // a set that is served.
                return NoPipeline();
            }

            if (dependencies is MessageDependencies { HandlerArray.Length: 0, IndirectHandlerArray.Length: 0 })
            {
                // The set selects no handler; reaching nobody is not an error and needs no
                // plan. Not slotted, for the same reason the null case is not.
                return NoPipeline();
            }

            var (plan, planDirect) = AdmitGroupedPlan(materialized, dependencies);
            slot = new GroupedSlot(materialized, canonical, plan, planDirect);
            _cachedGroupedSlot = slot;
        }

        var groupedPlan = slot.GroupedPlan;

        // A plan compiled for this exact set already knows its participants; the filtering
        // plan works them out from the set it is handed.
        if (groupedPlan.FilterGroups is not null)
        {
            return slot.PlanDirect
                ? groupedPlan.ExecuteFilteredDirect((TEvent)message, context, serviceProvider, slot.Groups)
                : groupedPlan.ExecuteFiltered((TEvent)message, context, serviceProvider, slot.Groups);
        }

        return slot.PlanDirect
            ? groupedPlan.ExecuteDirect((TEvent)message, context, serviceProvider)
            : groupedPlan.Execute((TEvent)message, context, serviceProvider);
    }

    /// <summary>
    /// Decides which compiled plan serves one group set, or fails the publish when none
    /// verifiably does.
    /// </summary>
    /// <param name="groups">The group set being decided for.</param>
    /// <param name="dependencies">The participants that set selects.</param>
    /// <returns>The plan and whether it may construct participants itself.</returns>
    /// <remarks>
    /// The same question the constructor answers for the default set, asked once per set as
    /// its slot is filled rather than on every publish.
    /// </remarks>
    private (StagedBroadcastPlan<TEvent> Plan, bool Direct) AdmitGroupedPlan(
        string[] groups, IMessageDependencies dependencies)
    {
        var plan = GeneratedDispatchRoots.FindBroadcastPlan(typeof(TEvent), groups) as StagedBroadcastPlan<TEvent>;

        if (plan is not null)
        {
            VerifyComposition(dependencies, plan.Composition);
        }
        else
        {
            // No plan was compiled for this set — every call site passed its groups as a
            // runtime value, or nobody ever spelled this particular set. The filtering plan
            // can serve any set, but only if it is checked against the participants of the
            // groups it covers: that is the one set reproducing everything its body holds.
            plan = GeneratedDispatchRoots.FindFilteredBroadcastPlan(typeof(TEvent)) as StagedBroadcastPlan<TEvent>;

            if (plan?.FilterGroups is not { } covered)
            {
                throw UnplannedDispatch.ForUnplannedGroupSet(typeof(TEvent), groups);
            }

            if (_factory.Find(typeof(TEvent), covered) is not { } full)
            {
                throw UnplannedDispatch.ForUnplannedGroupSet(typeof(TEvent), groups);
            }

            VerifyComposition(full, plan.Composition);
        }

        var direct = plan.SupportsDirectConstruction
                     && _factory is MessageDependenciesFactory typedFactory
                     && StagedPlanGate.AllPlainTransient(typedFactory, plan.Composition);

        return (plan, direct);
    }

    /// <summary>
    /// Verifies one live composition against what a plan compiled, failing the publish
    /// with the precise reason when they cannot be reconciled.
    /// </summary>
    /// <param name="dependencies">The live participants.</param>
    /// <param name="composition">The pipeline the plan was compiled against.</param>
    private static void VerifyComposition(IMessageDependencies dependencies, StagedPlanKey composition)
    {
        if (dependencies is not MessageDependencies fast)
        {
            throw UnplannedDispatch.ForForeignFactory(typeof(TEvent));
        }

        if (fast.ForcedMemoization)
        {
            throw UnplannedDispatch.ForMemoizedInstances(typeof(TEvent));
        }

        if (!StagedPlanGate.Matches(fast, composition))
        {
            throw UnplannedDispatch.ForDivergedBroadcastComposition(typeof(TEvent), fast, composition);
        }
    }

    /// <summary>
    /// Completes a publish that reaches nobody.
    /// </summary>
    /// <returns>A completed task.</returns>
    /// <remarks>
    /// The outcome is the same whether no composition serves the event type or the groups
    /// asked for select no handler: publishing to nobody is not an error at run time.
    /// </remarks>
    private static ValueTask NoPipeline() => default;

    /// <summary>
    /// One group set together with everything decided for it.
    /// </summary>
    /// <param name="groups">The group names this slot was built for.</param>
    /// <param name="canonical">The canonical set it came from, when it came from one.</param>
    /// <param name="plan">The plan serving the set.</param>
    /// <param name="planDirect">Whether that plan may construct participants itself.</param>
    private sealed class GroupedSlot(
        string[] groups,
        GroupSet? canonical,
        StagedBroadcastPlan<TEvent> plan,
        bool planDirect)
    {
        /// <summary>
        /// The group names this slot was built for.
        /// </summary>
        public readonly string[] Groups = groups;

        /// <summary>
        /// The canonical set this slot was built from, or <c>null</c>.
        /// </summary>
        public readonly GroupSet? Canonical = canonical;

        /// <summary>
        /// The plan serving this group set.
        /// </summary>
        public readonly StagedBroadcastPlan<TEvent> GroupedPlan = plan;

        /// <summary>
        /// Whether the plan may construct participants itself in this container.
        /// </summary>
        public readonly bool PlanDirect = planDirect;
    }
}
