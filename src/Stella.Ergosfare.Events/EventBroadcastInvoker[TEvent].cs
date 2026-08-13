using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;
using Stella.Ergosfare.Core.Internal.Factories;
using Stella.Ergosfare.Core.Internal.Mediator;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events;

internal sealed class EventBroadcastInvoker<TEvent> : IEventBroadcastInvoker
    where TEvent : notnull
{
    // One copy per closed event type is deliberate — the invoker itself is per-event-type.
    // ReSharper disable once StaticMemberInGenericType
    private static readonly string[] EmptyGroups = [];

    /// <summary>
    /// Publishes against the dispatch engine: the concrete machinery is known by
    /// construction, so every publish runs the fast lane against the invoker-cached
    /// pipeline plan and the caller's scope provider — no per-publish composition lookup.
    /// An externally owned context (nested publish) is used as-is; the caller controls its
    /// lifetime, so nothing is rented and nothing is returned.
    /// </summary>
    public ValueTask Publish(object @event, EventMediationSettings? settings, CancellationToken cancellationToken,
        MessageDispatchEngine engine, IServiceProvider serviceProvider,
        ErgosfareContext? externalContext = null,
        IEnumerable<string>? groupsOverride = null)
    {
        var pooled = externalContext is null
            ? ErgosfareContextPool.Rent(settings?.Items, cancellationToken)
            : null;
        var context = externalContext ?? pooled!;

        ValueTask task;

        try
        {
            var groups = groupsOverride ?? settings?.Filters.Groups;
            var groupless = groups is null or List<string> { Count: 0 } or string[] { Length: 0 } or GroupSet { Count: 0 };
            var dependencies = groupless
                ? GetPlan(engine.DependenciesFactory)
                : GetGroupedPlan(engine.DependenciesFactory, groups!);

            // Compiled plan first: the whole pipeline as straight-line calls. Ahead of the
            // runtime lane because a plan can exist for an interceptorless broadcast too —
            // a plugin's observers live in the plan body, and the loop below would deliver
            // the event without ever running them.
            task = groupless && _usePlan
                ? _usePlanDirect
                    ? StagedPlan!.ExecuteDirect((TEvent)@event, context, serviceProvider)
                    : StagedPlan!.Execute((TEvent)@event, context, serviceProvider)
                : BroadcastMediation<TEvent>.Deliver(
                    (TEvent)@event, dependencies, context, serviceProvider,
                    settings?.ThrowIfNoHandlerFound ?? false);
        }
        catch
        {
            if (pooled is not null)
            {
                ErgosfareContextPool.Return(pooled);
            }

            throw;
        }

        if (pooled is null)
        {
            return task;
        }

        if (task.IsCompletedSuccessfully)
        {
            ErgosfareContextPool.Return(pooled);
            return default;
        }

        return AwaitAndReturn(task, pooled);

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
    /// The group-less pipeline plan for <typeparamref name="TEvent"/>, cached on the
    /// invoker. A composition is settled before the container is built, so the plan never
    /// goes stale. Races are benign: concurrent writers publish equivalent state.
    /// </summary>
    private IMessageDependencies? _cachedDependencies;
    private MessageDependenciesFactory? _cachedFactory;

    /// <summary>
    /// The compiled plan for this event type, resolved once per closed invoker. Null when the
    /// generator modeled no plan — an unintercepted broadcast, or one whose handler set it
    /// could not model exactly.
    /// </summary>
    /// <remarks>
    /// Named as its closed type, not held erased: the broadcast plan family carries the same
    /// <c>notnull</c> constraint a publish does, so no cast stands between the lookup and the
    /// call.
    /// </remarks>
    // ReSharper disable once StaticMemberInGenericType
    private static readonly StagedBroadcastPlan<TEvent>? StagedPlan =
        GeneratedDispatchRoots.FindBroadcastPlan(typeof(TEvent)) as StagedBroadcastPlan<TEvent>;

    /// <summary>
    /// Whether the live composition is the one <see cref="StagedPlan"/> was baked against,
    /// and whether its direct-construction variant qualifies. Decided with the dependency
    /// cache — once per (invoker, factory) — because the gate answers a question about the
    /// composition, and the composition is frozen by the first dispatch.
    /// </summary>
    private bool _usePlan;
    private bool _usePlanDirect;

    private IMessageDependencies? GetPlan(
        Core.Abstractions.Factories.IMessageDependenciesFactory dependenciesFactory)
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            var cached = _cachedDependencies;

            // The invoker is process-wide (one per event type) while factories are
            // per-container — the factory reference is part of the cache key, so one
            // container's plan (which may pin that container's root provider for
            // memoized, all-singleton pipelines) is never served to another container.
            // Multiple containers ping-ponging simply rebuild the single slot; the
            // factory's own per-container cache keeps that cheap.
            if (cached is not null && ReferenceEquals(_cachedFactory, typedFactory))
            {
                return cached;
            }

            var dependencies = BuildPlan(typedFactory, EmptyGroups);

            if (dependencies is null)
            {
                // A message with no composition is not cached here: the catalog already
                // caches its own negative lookup per type, so the repeat cost is a
                // dictionary hit — and leaving the slot untouched keeps the cache fields
                // consistent for concurrent readers, which a "cached null" could not.
                return null;
            }

            _usePlan = StagedPlan is not null
                       && dependencies is MessageDependencies { MemoizedInstances: false } fastDependencies
                       && StagedPlanGate.Matches(fastDependencies, StagedPlan.Composition);
            _usePlanDirect = _usePlan
                             && StagedPlan!.SupportsDirectConstruction
                             && StagedPlanGate.AllPlainTransient(typedFactory, StagedPlan.Composition);

            _cachedDependencies = dependencies;
            _cachedFactory = typedFactory;
            return dependencies;
        }

        return BuildPlan(dependenciesFactory, EmptyGroups);
    }

    /// <summary>
    /// Grouped counterpart of <see cref="GetPlan"/>: a single last-used
    /// (factory, group set) slot serves the overwhelmingly common shape — one stable
    /// group set per event type — with an ordinal element-wise compare instead of a
    /// per-publish key materialization. The dependencies come from the same factory call
    /// the Mediate path would make, so the group-filtered pipeline is identical; a slot
    /// miss (alternating group sets, another container) only rebuilds
    /// through the factory's own process-wide dependency cache. The slot snapshots the
    /// caller's group sequence, so later caller-side mutation of a reused settings
    /// instance is seen as a different group set, never as a stale hit.
    /// </summary>
    private GroupedPlanSlot? _cachedGroupedPlan;

    private sealed class GroupedPlanSlot(
        MessageDependenciesFactory factory,
        string[] groups,
        GroupSet? canonical,
        IMessageDependencies dependencies)
    {
        public readonly MessageDependenciesFactory Factory = factory;
        public readonly string[] Groups = groups;
        public readonly GroupSet? Canonical = canonical;
        public readonly IMessageDependencies Dependencies = dependencies;
    }

    private IMessageDependencies? GetGroupedPlan(
        Core.Abstractions.Factories.IMessageDependenciesFactory dependenciesFactory,
        IEnumerable<string> groups)
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            var slot = _cachedGroupedPlan;

            // Deliberate: groups is matched allocation-free first and only materialized on a slot miss.
            // ReSharper disable once PossibleMultipleEnumeration
            if (slot is not null
                && ReferenceEquals(slot.Factory, typedFactory)
                && GroupSlotMatch.Matches(groups, slot.Groups, slot.Canonical))
            {
                return slot.Dependencies;
            }

            // A canonical set contributes its immutable name array directly.
            var canonical = groups as GroupSet;
            // ReSharper disable once PossibleMultipleEnumeration
            var materialized = canonical?.Names ?? [.. groups];
            var dependencies = BuildPlan(typedFactory, materialized);

            if (dependencies is null)
            {
                // Not slotted, for the reason GetPlan gives.
                return null;
            }

            _cachedGroupedPlan = new GroupedPlanSlot(
                typedFactory, materialized, canonical, dependencies);
            return dependencies;
        }

        return BuildPlan(dependenciesFactory, [.. groups]);
    }

    /// <summary>
    /// The pipeline plan for <typeparamref name="TEvent"/>, or <c>null</c> when the event
    /// type has no descriptor at all.
    /// </summary>
    /// <remarks>
    /// Null rather than an exception on purpose: an unregistered event type is the same
    /// "nothing will handle this" as a registered one whose handlers are all filtered out,
    /// and both are the caller's <see cref="EventMediationSettings.ThrowIfNoHandlerFound"/>
    /// to answer. Only the publish site knows what the caller asked for, so the decision
    /// belongs there and not here.
    /// </remarks>
    private static IMessageDependencies? BuildPlan(
        Core.Abstractions.Factories.IMessageDependenciesFactory factory,
        string[] groups)
        // Non-throwing by design: an event nobody subscribes to has no pipeline, and the
        // caller's throwIfNoHandlerFound flag — not this lookup — decides what that means.
        => factory.Find(typeof(TEvent), groups);

}
