using Stella.Ergosfare.Core.Abstractions.Attributes;

namespace Stella.Ergosfare.Core.Test;

/// <summary>
/// Unit tests for the runtime <see cref="Discovery"/> helper — the single source of truth
/// for how the discovery attributes and key patterns compose on the reflection paths.
/// </summary>
public class DiscoveryTests
{
    private sealed class Untagged;

    [DiscoveryKey("reporting.daily", "audit")]
    private sealed class Keyed;

    [DiscoveryKey("", "debug")]
    private sealed class KeyedWithDefault;

    [ExcludeFromDiscovery]
    private sealed class Excluded;

    [Theory]
    [InlineData("", "", true)]
    [InlineData("a", "", false)]
    [InlineData("", "a", false)]
    [InlineData("a", "a", true)]
    [InlineData("a", "A", false)]
    [InlineData("abc", "a*", true)]
    [InlineData("a", "a*", true)]
    [InlineData("b", "a*", false)]
    [InlineData("", "*", true)]
    [InlineData("anything", "*", true)]
    [InlineData("reporting.daily", "reporting.*", true)]
    [InlineData("reporting", "reporting.*", false)]
    public void MatchesKey_AppliesExactOrPrefixSemantics(string key, string pattern, bool expected)
        => Assert.Equal(expected, Discovery.MatchesKey(key, pattern));

    [Fact]
    public void GetKeys_UntaggedTypeCarriesTheDefaultKey()
        => Assert.Equal([DiscoveryKeyAttribute.DefaultKey], Discovery.GetKeys(typeof(Untagged)));

    [Fact]
    public void GetKeys_TaggedTypeCarriesItsDeclaredKeys()
        => Assert.Equal(["reporting.daily", "audit"], Discovery.GetKeys(typeof(Keyed)));

    [Fact]
    public void Matches_UntaggedType_MatchesDefaultAndStarOnly()
    {
        Assert.True(Discovery.Matches(typeof(Untagged), ""));
        Assert.True(Discovery.Matches(typeof(Untagged), "*"));
        Assert.False(Discovery.Matches(typeof(Untagged), "reporting"));
    }

    [Fact]
    public void Matches_KeyedType_IsGatedOutOfDefaultDiscovery()
    {
        Assert.False(Discovery.Matches(typeof(Keyed), ""));
        Assert.True(Discovery.Matches(typeof(Keyed), "audit"));
        Assert.True(Discovery.Matches(typeof(Keyed), "reporting.*"));
        Assert.True(Discovery.Matches(typeof(Keyed), "*"));
    }

    [Fact]
    public void Matches_EmptyStringKey_KeepsTypeInDefaultDiscovery()
    {
        Assert.True(Discovery.Matches(typeof(KeyedWithDefault), ""));
        Assert.True(Discovery.Matches(typeof(KeyedWithDefault), "debug"));
    }

    [Fact]
    public void Matches_ExcludedType_NeverMatches()
    {
        Assert.True(Discovery.IsExcluded(typeof(Excluded)));
        Assert.False(Discovery.Matches(typeof(Excluded), ""));
        Assert.False(Discovery.Matches(typeof(Excluded), "*"));
    }
}
