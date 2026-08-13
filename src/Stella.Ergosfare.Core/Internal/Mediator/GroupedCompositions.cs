using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One message type's grouped compositions, behind a single last-used slot.
/// </summary>
/// <remarks>
/// <para>
/// The group filter used to be part of an executor's identity: the lookup in front of the
/// executor carried the group set in its key, so a grouped dispatch cost a composite-keyed
/// dictionary and, on a miss, a joined string key. It is a dispatch argument now, which
/// means the question "which composition does this filter select" moved here — the same
/// place, and the same answer, the publishing table already used.
/// </para>
/// <para>
/// One slot rather than a dictionary because the overwhelmingly common grouped caller
/// dispatches one message type under one stable filter. Alternating filters miss the slot
/// and re-ask the factory, which caches per (type, groups) itself — so a miss is a
/// dictionary hit there rather than a rebuilt graph.
/// </para>
/// </remarks>
internal sealed class GroupedCompositions(IMessageDependenciesFactory dependenciesFactory, Type messageType)
{
    private Entry? _slot;

    /// <summary>
    /// The composition selected by <paramref name="groups"/>. The caller's sequence is
    /// matched against the slot allocation-free and only materialized on a miss, so a
    /// steady grouped caller never allocates.
    /// </summary>
    internal Entry Resolve(IEnumerable<string> groups)
    {
        // A foreign factory keeps the original per-dispatch behavior: nothing is slotted,
        // because nothing about a foreign implementation promises the same answer twice.
        if (dependenciesFactory is not MessageDependenciesFactory)
        {
            var materializedForeign = Materialize(groups);

            return new Entry(materializedForeign, null,
                dependenciesFactory.Create(messageType, materializedForeign), null);
        }

        var slot = _slot;

        // Deliberate: groups is matched allocation-free first and only materialized on a miss.
        // ReSharper disable once PossibleMultipleEnumeration
        if (slot is not null && GroupSlotMatch.Matches(groups, slot.Groups, slot.Canonical))
        {
            return slot;
        }

        // ReSharper disable once PossibleMultipleEnumeration
        return ResolveSlow(groups);
    }

    private Entry ResolveSlow(IEnumerable<string> groups)
    {
        // A canonical set contributes its immutable name array directly — the refresh
        // allocates nothing for it.
        var canonical = groups as GroupSet;
        var materialized = canonical?.Names ?? Materialize(groups);
        var dependencies = dependenciesFactory.Create(messageType, materialized);

        // Races are benign: both writers publish equivalent, idempotent state, and the
        // entry is immutable so a reader that observes the reference sees every field.
        var entry = new Entry(materialized, canonical, dependencies, dependencies as MessageDependencies);
        _slot = entry;

        return entry;
    }

    /// <summary>
    /// Snapshots the caller's sequence exactly once: the same array both matches the slot
    /// and builds the composition, so a lazy or unstable enumerable can never select one
    /// composition and be recorded as another.
    /// </summary>
    private static string[] Materialize(IEnumerable<string> groups) => [.. groups];

    /// <summary>
    /// An immutable (filter, composition) pair. <see cref="Fast"/> is the concrete view the
    /// single-handler fast lane needs, resolved with the composition so the grouped hot
    /// path reads a field instead of testing a type.
    /// </summary>
    internal sealed class Entry(
        string[] groups,
        GroupSet? canonical,
        IMessageDependencies dependencies,
        MessageDependencies? fast)
    {
        public readonly string[] Groups = groups;
        public readonly GroupSet? Canonical = canonical;
        public readonly IMessageDependencies Dependencies = dependencies;
        public readonly MessageDependencies? Fast = fast;
    }
}
