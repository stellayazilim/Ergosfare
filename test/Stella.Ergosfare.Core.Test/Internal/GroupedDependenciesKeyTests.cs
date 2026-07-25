using Stella.Ergosfare.Core.Internal.Caching;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Stella.Ergosfare.Core.Test.Internal;

/// <summary>
/// Unit tests for <see cref="GroupedDependenciesKey"/> equality and hashing semantics —
/// the struct keys the process-wide grouped dependency caches.
/// </summary>
public class GroupedDependenciesKeyTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Equals_ShouldBeTrue_ForSameTypeAndGroups()
    {
        var left = new GroupedDependenciesKey(typeof(StubMessage), ["a", "b"]);
        var right = new GroupedDependenciesKey(typeof(StubMessage), ["a", "b"]);

        Assert.True(left.Equals(right));
        Assert.True(left.Equals((object) right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Equals_ShouldBeFalse_ForDifferentMessageType()
    {
        var left = new GroupedDependenciesKey(typeof(StubMessage), ["a"]);
        var right = new GroupedDependenciesKey(typeof(StubUnrelatedMessage), ["a"]);

        Assert.False(left.Equals(right));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Equals_ShouldBeFalse_ForDifferentGroupContent()
    {
        var left = new GroupedDependenciesKey(typeof(StubMessage), ["a", "b"]);
        var right = new GroupedDependenciesKey(typeof(StubMessage), ["a", "c"]);

        Assert.False(left.Equals(right));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Equals_ShouldBeFalse_ForDifferentGroupCount()
    {
        var left = new GroupedDependenciesKey(typeof(StubMessage), ["a"]);
        var right = new GroupedDependenciesKey(typeof(StubMessage), ["a", "b"]);

        Assert.False(left.Equals(right));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Equals_ShouldBeFalse_ForNonKeyObject()
    {
        var key = new GroupedDependenciesKey(typeof(StubMessage), ["a"]);

        // ReSharper disable once SuspiciousTypeConversion.Global
        Assert.False(key.Equals("not a key"));
    }
}
