using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// What came of asking whether a compiled plan serves one group set.
/// </summary>
/// <param name="plan">
/// The plan, held without its type because the dispatch that asked knows what to cast it
/// to; <c>null</c> when no plan serves the set.
/// </param>
/// <param name="direct">
/// Whether this container also qualifies for the plan's direct-construction variant.
/// </param>
/// <remarks>The default value means no plan serves the set.</remarks>
internal readonly struct GroupedPlanAdmission(object? plan, bool direct)
{
    /// <summary>
    /// The plan serving the group set, or <c>null</c>.
    /// </summary>
    public readonly object? Plan = plan;

    /// <summary>
    /// Whether the plan's direct-construction variant may be used.
    /// </summary>
    public readonly bool Direct = direct;
}

/// <summary>
/// One message type's participants per group set, remembered behind a single last-used
/// slot.
/// </summary>
/// <param name="dependenciesFactory">The factory that builds the participants.</param>
/// <param name="messageType">The message type this instance answers for.</param>
/// <param name="admitPlan">
/// Asks the owning dispatch whether a compiled plan serves a group set. Called once per set
/// while its entry is built, and <c>null</c> when the owner has no plans to offer.
/// </param>
/// <remarks>
/// A single slot rather than a dictionary, because a caller that dispatches under groups
/// almost always uses one stable filter. Alternating filters miss the slot and ask the
/// factory again, which keeps its own per-(type, groups) cache — so a miss costs a
/// dictionary lookup, not a rebuilt pipeline.
/// </remarks>
internal sealed class GroupedCompositions(
    IMessageDependenciesFactory dependenciesFactory,
    Type messageType,
    Func<string[], IMessageDependencies, GroupedPlanAdmission>? admitPlan = null)
{
    private Entry? _slot;

    /// <summary>
    /// Returns the entry <paramref name="groups"/> selects.
    /// </summary>
    /// <param name="groups">The groups the dispatch asked for.</param>
    /// <returns>The participants and plan decision for that set.</returns>
    /// <remarks>
    /// The caller's sequence is compared against the slot without being copied, and copied
    /// only when it misses, so a caller reusing one filter allocates nothing.
    /// </remarks>
    internal Entry Resolve(IEnumerable<string> groups)
    {
        // A factory from outside gets the un-slotted behavior: nothing about a foreign
        // implementation promises the same answer twice.
        if (dependenciesFactory is not MessageDependenciesFactory)
        {
            var materializedForeign = Materialize(groups);

            return new Entry(materializedForeign, null,
                dependenciesFactory.Create(messageType, materializedForeign), null, default);
        }

        var slot = _slot;

        // Deliberate: groups is compared without being copied first, and copied only on a
        // miss.
        // ReSharper disable once PossibleMultipleEnumeration
        if (slot is not null && GroupSlotMatch.Matches(groups, slot.Groups, slot.Canonical))
        {
            return slot;
        }

        // ReSharper disable once PossibleMultipleEnumeration
        return ResolveSlow(groups);
    }

    /// <summary>
    /// Builds and publishes the entry for a group set the slot did not hold.
    /// </summary>
    /// <param name="groups">The groups the dispatch asked for.</param>
    /// <returns>The new entry, now in the slot.</returns>
    private Entry ResolveSlow(IEnumerable<string> groups)
    {
        // A canonical set hands over its own immutable array, so it costs no copy.
        var canonical = groups as GroupSet;
        var materialized = canonical?.Names ?? Materialize(groups);
        var dependencies = dependenciesFactory.Create(messageType, materialized);

        // The plan is decided here, before the entry is published: one decision per group
        // set, so the dispatch path only ever reads a settled entry.
        var admission = admitPlan is null ? default : admitPlan(materialized, dependencies);

        // Two threads racing here both publish equivalent state, and the entry is immutable,
        // so a reader that sees the reference sees every field of it.
        var entry = new Entry(materialized, canonical, dependencies, dependencies as MessageDependencies, admission);
        _slot = entry;

        return entry;
    }

    /// <summary>
    /// Copies the caller's sequence into an array.
    /// </summary>
    /// <param name="groups">The sequence to copy.</param>
    /// <returns>The copy.</returns>
    /// <remarks>
    /// Done exactly once, and the same array both matches the slot and builds the
    /// participants — otherwise a sequence that yields different values each time it is
    /// enumerated could select one pipeline and be recorded as another.
    /// </remarks>
    private static string[] Materialize(IEnumerable<string> groups) => [.. groups];

    /// <summary>
    /// One group set together with everything decided for it.
    /// </summary>
    /// <param name="groups">The group names this entry was built for.</param>
    /// <param name="canonical">The canonical set it came from, when it came from one.</param>
    /// <param name="dependencies">The participants for the set.</param>
    /// <param name="fast">
    /// The same participants as their concrete type when they are one, so the single-handler
    /// path reads a field instead of testing a type.
    /// </param>
    /// <param name="admission">The plan decision for the set.</param>
    internal sealed class Entry(
        string[] groups,
        GroupSet? canonical,
        IMessageDependencies dependencies,
        MessageDependencies? fast,
        GroupedPlanAdmission admission)
    {
        /// <summary>
        /// The group names this entry was built for.
        /// </summary>
        public readonly string[] Groups = groups;

        /// <summary>
        /// The canonical set this entry was built from, or <c>null</c>.
        /// </summary>
        public readonly GroupSet? Canonical = canonical;

        /// <summary>
        /// The participants for this group set.
        /// </summary>
        public readonly IMessageDependencies Dependencies = dependencies;

        /// <summary>
        /// The participants as their concrete type, or <c>null</c> when they came from
        /// elsewhere.
        /// </summary>
        public readonly MessageDependencies? Fast = fast;

        /// <summary>
        /// The plan serving this group set and whether its direct-construction variant
        /// qualifies. Decided and carried with the participants, so the dispatch path
        /// cannot pair a plan with a pipeline it was not checked against.
        /// </summary>
        public readonly GroupedPlanAdmission Admission = admission;
    }
}
