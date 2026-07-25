using Stella.Ergosfare.Core.Internal.Caching;

namespace Stella.Ergosfare.Core.Test.Internal;

/// <summary>
/// Unit tests for <see cref="LruCacheStrategy"/>, covering hit/miss, eviction order,
/// explicit eviction, clearing and disposal.
/// </summary>
public class LruCacheStrategyTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void TryGet_ShouldReturnFalse_WhenKeyMissing()
    {
        using var cache = new LruCacheStrategy();

        var found = cache.TryGet("missing", out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Add_ShouldStoreValue_AndTryGetShouldReturnIt()
    {
        using var cache = new LruCacheStrategy();

        cache.Add("key", "value");

        Assert.True(cache.TryGet("key", out var value));
        Assert.Equal("value", value);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Evict_ShouldRemoveKey()
    {
        using var cache = new LruCacheStrategy();
        cache.Add("key", "value");

        cache.Evict("key");

        Assert.False(cache.TryGet("key", out _));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Clear_ShouldRemoveAllEntries()
    {
        using var cache = new LruCacheStrategy();
        cache.Add("a", 1);
        cache.Add("b", 2);

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGet("a", out _));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Add_ShouldEvictLeastRecentlyUsed_WhenMaxSizeReached()
    {
        using var cache = new LruCacheStrategy(maxSize: 2);

        cache.Add("first", 1);
        Thread.Sleep(20);
        cache.Add("second", 2);
        Thread.Sleep(20);

        // Touch "first" so "second" becomes the least recently used entry.
        Assert.True(cache.TryGet("first", out _));
        Thread.Sleep(20);

        cache.Add("third", 3);

        Assert.True(cache.TryGet("first", out _));
        Assert.False(cache.TryGet("second", out _));
        Assert.True(cache.TryGet("third", out _));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Dispose_ShouldClearCache()
    {
        var cache = new LruCacheStrategy();
        cache.Add("key", "value");

        cache.Dispose();

        Assert.Equal(0, cache.Count);
    }
}
