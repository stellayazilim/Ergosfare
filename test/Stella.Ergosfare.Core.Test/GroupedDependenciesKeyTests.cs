using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Internal.Caching;

namespace Stella.Ergosfare.Core.Test;

/// <summary>
/// The composite key the grouped dependency cache is keyed by: message type plus the
/// requested group names, in order. Two dispatches share a cached composition exactly when
/// their keys compare equal, so what this type calls "the same request" is what decides which
/// handlers a grouped dispatch gets.
/// </summary>
public class GroupedDependenciesKeyTests
{
    private sealed record Ping : IMessage;

    private sealed record Pong : IMessage;

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void SameTypeAndSameNames_IsTheSameRequest()
    {
        var key = new GroupedDependenciesKey(typeof(Ping), ["reporting", "auditing"]);
        var same = new GroupedDependenciesKey(typeof(Ping), ["reporting", "auditing"]);

        Assert.Equal(key, same);
        Assert.Equal(key.GetHashCode(), same.GetHashCode());
        Assert.True(key.Equals((object) same));

        // The key carries what it was built from, which is what a cache entry is read back
        // against when a slot is refreshed.
        Assert.Equal(typeof(Ping), key.MessageType);
        Assert.Equal(new[] { "reporting", "auditing" }, key.Groups);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ADifferentMessage_ADifferentCount_OrADifferentName_IsADifferentRequest()
    {
        var key = new GroupedDependenciesKey(typeof(Ping), ["reporting", "auditing"]);

        Assert.NotEqual(key, new GroupedDependenciesKey(typeof(Pong), ["reporting", "auditing"]));
        Assert.NotEqual(key, new GroupedDependenciesKey(typeof(Ping), ["reporting"]));
        Assert.NotEqual(key, new GroupedDependenciesKey(typeof(Ping), ["reporting", "billing"]));

        // Ordinal and order-sensitive, matching dispatch-time group semantics exactly — the
        // same two names in the other order select a different composition.
        Assert.NotEqual(key, new GroupedDependenciesKey(typeof(Ping), ["auditing", "reporting"]));
        Assert.NotEqual(key, new GroupedDependenciesKey(typeof(Ping), ["Reporting", "auditing"]));

        Assert.False(key.Equals("not a key"));
    }
}
