using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Core.Test;

/// <summary>
/// The canonical group filter: interning identity, order sensitivity, snapshot semantics
/// and the empty-set singleton — the contracts the grouped dispatch caches' reference
/// fast path relies on.
/// </summary>
public class GroupSetTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Of_InternsEqualSequences_ToTheSameInstance()
    {
        var first = GroupSet.Of("groupset.a", "groupset.b");
        var second = GroupSet.Of("groupset.a", "groupset.b");

        Assert.Same(first, second);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Of_IsOrderSensitive()
    {
        // Order is significant at dispatch time (it keys the composite stores), so the
        // canonical sets must distinguish it too.
        var forward = GroupSet.Of("groupset.first", "groupset.second");
        var reversed = GroupSet.Of("groupset.second", "groupset.first");

        Assert.NotSame(forward, reversed);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Of_WithNoGroups_ReturnsTheEmptySingleton()
    {
        Assert.Same(GroupSet.Empty, GroupSet.Of());
        Assert.Empty(GroupSet.Empty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Of_RejectsNullInput()
    {
        Assert.Throws<ArgumentNullException>(static () => GroupSet.Of(null!));
        Assert.Throws<ArgumentException>(static () => GroupSet.Of("groupset.valid", null!));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Of_SnapshotsTheInputArray()
    {
        var names = new[] { "groupset.snap.a", "groupset.snap.b" };
        var set = GroupSet.Of(names);

        names[0] = "groupset.snap.mutated";

        // The set is immutable: later mutation of the caller's array must not leak in.
        Assert.Equal(["groupset.snap.a", "groupset.snap.b"], set);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void GroupSet_IsAReadOnlyListOfItsNames()
    {
        var set = GroupSet.Of("groupset.list.a", "groupset.list.b");

        Assert.Equal(2, set.Count);
        Assert.Equal("groupset.list.a", set[0]);
        Assert.Equal("groupset.list.b", set[1]);
        Assert.Equal(["groupset.list.a", "groupset.list.b"], set.ToArray());
    }
}
