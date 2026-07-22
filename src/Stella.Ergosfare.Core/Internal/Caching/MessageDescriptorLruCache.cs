using System.Collections.Concurrent;
using Stella.Ergosfare.Core.Abstractions.Caching;

namespace Stella.Ergosfare.Core.Internal.Caching;

/// <summary>
/// LRU (Least Recently Used) cache strategy.
/// </summary>
public sealed class LruCacheStrategy : IDescriptorCacheStrategy
{
    private readonly ConcurrentDictionary<string, LruCacheEntry> _cache = new();
    private readonly uint _maxSize;
    private readonly object _cleanupLock = new();

    public LruCacheStrategy(uint maxSize = 100)
    {
        _maxSize = maxSize;
    }

    public bool TryGet(string key, out object? value)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            entry.Touch();
            value = entry.Value;
            return true;
        }
        
        value = null;
        return false;
    }

    public void Add(string key, object value)
    {
        if ((uint)_cache.Count >= _maxSize)
        {
            EvictLeastRecentlyUsed();
        }
        
        _cache[key] = new LruCacheEntry(value);
    }

    public void Clear() => _cache.Clear();
    
    public void Evict(string key) => _cache.TryRemove(key, out _);
    
    public int Count => _cache.Count;

    private void EvictLeastRecentlyUsed()
    {
        lock (_cleanupLock)
        {
            // Called when the cache is at (or beyond) capacity, before a new entry is
            // added: evicting down to maxSize - 1 keeps the post-add count within
            // maxSize. The previous <= guard returned early at exactly maxSize, letting
            // the cache grow to maxSize + 1 before eviction kicked in.
            if ((uint)_cache.Count < _maxSize) return;

            var toRemove = _cache
                .OrderBy(x => x.Value.LastAccessed)
                .Take((int)(_cache.Count - _maxSize + 1))
                .Select(x => x.Key)
                .ToList();

            foreach (var key in toRemove)
            {
                _cache.TryRemove(key, out _);
            }
        }
    }

    public void Dispose()
    {
        Clear();
    }

    private sealed class LruCacheEntry
    {
        public object Value { get; }
        public DateTime LastAccessed { get; private set; }

        public LruCacheEntry(object value)
        {
            Value = value;
            LastAccessed = DateTime.UtcNow;
        }

        public void Touch() => LastAccessed = DateTime.UtcNow;
    }
}