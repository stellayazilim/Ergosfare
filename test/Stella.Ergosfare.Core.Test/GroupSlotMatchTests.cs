using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Core.Test;

/// <summary>
/// The last-used (groups → pipeline) slot check every dispatch surface shares. A false
/// positive here hands a dispatch the wrong pipeline — the filter is what decides which
/// handlers run — so each of the four shapes it special-cases is compared both ways.
/// </summary>
/// <remarks>
/// The shapes exist for allocation, not for semantics: <see cref="GroupSet"/>, array and
/// list are indexed without an enumerator, and anything else falls to the general walk. All
/// four must answer identically, which is what these assert by asking the same questions of
/// each.
/// </remarks>
public class GroupSlotMatchTests
{
    private static readonly string[] Cached = ["slot.a", "slot.b"];

    /// <summary>The general case: an enumerable with none of the fast shapes' interfaces.</summary>
    private static IEnumerable<string> Walked(params string[] names)
    {
        foreach (var name in names)
        {
            yield return name;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void CanonicalSet_MatchesOnReference_WithoutComparingNames()
    {
        var canonical = GroupSet.Of("slot.a", "slot.b");

        // The steady state for a caller reusing a static filter: one reference check.
        Assert.True(GroupSlotMatch.Matches(canonical, Cached, canonical));

        // A different set with the same names still matches — by content, one comparison
        // longer, which is what keeps an un-interned set correct.
        Assert.True(GroupSlotMatch.Matches(canonical, Cached, GroupSet.Of("slot.x")));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void NonSetSequence_NeverTakesTheReferenceShortcut()
    {
        // A list is compared by content every time, so a caller that mutates the same list
        // in place between dispatches is always observed.
        var reused = new List<string> { "slot.a", "slot.b" };

        Assert.True(GroupSlotMatch.Matches(reused, Cached, null));

        reused[1] = "slot.z";

        Assert.False(GroupSlotMatch.Matches(reused, Cached, null));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void EverySequenceShape_AgreesOnAMatch()
    {
        Assert.True(GroupSlotMatch.SequenceMatches(GroupSet.Of("slot.a", "slot.b"), Cached));
        Assert.True(GroupSlotMatch.SequenceMatches(new[] { "slot.a", "slot.b" }, Cached));
        Assert.True(GroupSlotMatch.SequenceMatches(new List<string> { "slot.a", "slot.b" }, Cached));
        Assert.True(GroupSlotMatch.SequenceMatches(Walked("slot.a", "slot.b"), Cached));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void EverySequenceShape_RejectsADifferentName()
    {
        Assert.False(GroupSlotMatch.SequenceMatches(GroupSet.Of("slot.a", "slot.z"), Cached));
        Assert.False(GroupSlotMatch.SequenceMatches(new[] { "slot.a", "slot.z" }, Cached));
        Assert.False(GroupSlotMatch.SequenceMatches(new List<string> { "slot.a", "slot.z" }, Cached));
        Assert.False(GroupSlotMatch.SequenceMatches(Walked("slot.a", "slot.z"), Cached));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void EverySequenceShape_RejectsADifferentLength_BothWays()
    {
        // Shorter than the snapshot: the indexed shapes answer from the count alone, the
        // walked one only finds out when the sequence ends early.
        Assert.False(GroupSlotMatch.SequenceMatches(GroupSet.Of("slot.a"), Cached));
        Assert.False(GroupSlotMatch.SequenceMatches(new[] { "slot.a" }, Cached));
        Assert.False(GroupSlotMatch.SequenceMatches(new List<string> { "slot.a" }, Cached));
        Assert.False(GroupSlotMatch.SequenceMatches(Walked("slot.a"), Cached));

        // Longer: a prefix match is not a match — the walked shape has to notice it ran past
        // the snapshot rather than stopping happy at its end.
        Assert.False(GroupSlotMatch.SequenceMatches(GroupSet.Of("slot.a", "slot.b", "slot.c"), Cached));
        Assert.False(GroupSlotMatch.SequenceMatches(new[] { "slot.a", "slot.b", "slot.c" }, Cached));
        Assert.False(GroupSlotMatch.SequenceMatches(new List<string> { "slot.a", "slot.b", "slot.c" }, Cached));
        Assert.False(GroupSlotMatch.SequenceMatches(Walked("slot.a", "slot.b", "slot.c"), Cached));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void EverySequenceShape_IsOrderSensitive()
    {
        // Order keys the composite store, so a reordered filter is a different filter.
        Assert.False(GroupSlotMatch.SequenceMatches(GroupSet.Of("slot.b", "slot.a"), Cached));
        Assert.False(GroupSlotMatch.SequenceMatches(new[] { "slot.b", "slot.a" }, Cached));
        Assert.False(GroupSlotMatch.SequenceMatches(new List<string> { "slot.b", "slot.a" }, Cached));
        Assert.False(GroupSlotMatch.SequenceMatches(Walked("slot.b", "slot.a"), Cached));
    }
}
