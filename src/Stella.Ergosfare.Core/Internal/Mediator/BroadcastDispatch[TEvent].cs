using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>The typed closure of <see cref="BroadcastDispatch"/>.</summary>
internal sealed class BroadcastDispatch<TEvent>(IMessageDependenciesFactory dependenciesFactory)
    : BroadcastDispatch
    where TEvent : notnull
{
    // One copy per closed message type is deliberate — the dispatch itself is per type.
    // ReSharper disable once StaticMemberInGenericType
    private static readonly string[] EmptyGroups = [];

    /// <summary>
    /// The compiled plan for this message type. Named as its closed type rather than held
    /// erased: the broadcast plan family carries the same <c>notnull</c> constraint a publish
    /// does, so no cast stands between the lookup and the call. Null when the generator
    /// modeled none — an unintercepted broadcast, or one whose handler set it could not model
    /// exactly.
    /// </summary>
    // ReSharper disable once StaticMemberInGenericType
    private static readonly StagedBroadcastPlan<TEvent>? Plan =
        GeneratedDispatchRoots.FindBroadcastPlan(typeof(TEvent)) as StagedBroadcastPlan<TEvent>;

    /// <summary>
    /// The group-less composition, resolved once. A composition is settled before the
    /// container is built, so it never goes stale; races are benign, since concurrent writers
    /// publish equivalent state.
    /// </summary>
    private IMessageDependencies? _cachedDependencies;

    /// <summary>
    /// Whether the live composition is the one <see cref="Plan"/> was baked against, and
    /// whether its direct-construction variant qualifies. Decided with the composition — once
    /// — because the gate asks a question about the composition, and that is frozen by the
    /// first dispatch.
    /// </summary>
    private bool _usePlan;
    private bool _usePlanDirect;

    private GroupedSlot? _cachedGroupedSlot;

    /// <inheritdoc />
    internal override ValueTask Publish(
        object message,
        ErgosfareContext context,
        IServiceProvider serviceProvider,
        IEnumerable<string>? groups,
        bool throwIfNoHandlerFound)
    {
        var groupless = groups is null or List<string> { Count: 0 } or string[] { Length: 0 } or GroupSet { Count: 0 };
        var dependencies = groupless ? GetDependencies() : GetGroupedDependencies(groups!);

        // Compiled plan first: the whole pipeline as straight-line calls. Ahead of the runtime
        // delivery because a plan can exist for an interceptorless broadcast too — a plugin's
        // observers live in the plan body, and the delivery below would reach every handler
        // without ever running them.
        return groupless && _usePlan
            ? _usePlanDirect
                ? Plan!.ExecuteDirect((TEvent)message, context, serviceProvider)
                : Plan!.Execute((TEvent)message, context, serviceProvider)
            : BroadcastMediation<TEvent>.Deliver(
                (TEvent)message, dependencies, context, serviceProvider, throwIfNoHandlerFound);
    }

    private IMessageDependencies? GetDependencies()
    {
        if (dependenciesFactory is not MessageDependenciesFactory typedFactory)
        {
            return Build(dependenciesFactory, EmptyGroups);
        }

        var cached = _cachedDependencies;

        if (cached is not null)
        {
            return cached;
        }

        var dependencies = Build(typedFactory, EmptyGroups);

        if (dependencies is null)
        {
            // A message with no composition is not cached: the catalog already caches its own
            // negative lookup per type, so the repeat cost is a dictionary hit — and leaving
            // the field untouched keeps it consistent for concurrent readers, which a cached
            // null could not.
            return null;
        }

        _usePlan = Plan is not null
                   && dependencies is MessageDependencies { MemoizedInstances: false } fast
                   && StagedPlanGate.Matches(fast, Plan.Composition);
        _usePlanDirect = _usePlan
                         && Plan!.SupportsDirectConstruction
                         && StagedPlanGate.AllPlainTransient(typedFactory, Plan.Composition);

        _cachedDependencies = dependencies;
        return dependencies;
    }

    /// <summary>
    /// Grouped counterpart of <see cref="GetDependencies"/>: a single last-used group-set slot
    /// serves the overwhelmingly common shape — one stable group set per message type — with
    /// an ordinal element-wise compare instead of a per-publish key materialization. The slot
    /// snapshots the caller's sequence, so later mutation of a reused settings instance is
    /// seen as a different group set, never as a stale hit.
    /// </summary>
    private IMessageDependencies? GetGroupedDependencies(IEnumerable<string> groups)
    {
        if (dependenciesFactory is not MessageDependenciesFactory typedFactory)
        {
            return Build(dependenciesFactory, [.. groups]);
        }

        var slot = _cachedGroupedSlot;

        // Deliberate: groups is matched allocation-free first and only materialized on a miss.
        // ReSharper disable once PossibleMultipleEnumeration
        if (slot is not null && GroupSlotMatch.Matches(groups, slot.Groups, slot.Canonical))
        {
            return slot.Dependencies;
        }

        // A canonical set contributes its immutable name array directly.
        var canonical = groups as GroupSet;
        // ReSharper disable once PossibleMultipleEnumeration
        var materialized = canonical?.Names ?? [.. groups];
        var dependencies = Build(typedFactory, materialized);

        if (dependencies is null)
        {
            // Not slotted, for the reason GetDependencies gives.
            return null;
        }

        _cachedGroupedSlot = new GroupedSlot(materialized, canonical, dependencies);
        return dependencies;
    }

    /// <summary>
    /// The composition for this message type, or <c>null</c> when the type has no descriptor
    /// at all.
    /// </summary>
    /// <remarks>
    /// Null rather than an exception on purpose: an unregistered message is the same "nothing
    /// will handle this" as a registered one whose handlers are all filtered out, and both are
    /// the caller's throw-if-none flag to answer. Only the publish site knows what the caller
    /// asked for.
    /// </remarks>
    private static IMessageDependencies? Build(IMessageDependenciesFactory factory, string[] groups)
        => factory.Find(typeof(TEvent), groups);

    private sealed class GroupedSlot(string[] groups, GroupSet? canonical, IMessageDependencies dependencies)
    {
        public readonly string[] Groups = groups;
        public readonly GroupSet? Canonical = canonical;
        public readonly IMessageDependencies Dependencies = dependencies;
    }
}
