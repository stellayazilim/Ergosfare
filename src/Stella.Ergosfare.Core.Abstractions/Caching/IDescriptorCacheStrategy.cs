namespace Stella.Ergosfare.Core.Abstractions.Caching;

/// <summary>
/// Strategy for caching objects by string key.
/// </summary>
public interface IDescriptorCacheStrategy : IDisposable
{
    /// <summary>
    /// Tries to get a cached object.
    /// </summary>
    bool TryGet(string key, out object? value);
    
    /// <summary>
    /// Adds an object to the cache.
    /// </summary>
    void Add(string key, object value);
    
    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    void Clear();
    
    /// <summary>
    /// Evicts a specific entry from cache.
    /// </summary>
    void Evict(string key);
    
    /// <summary>
    /// Gets the number of cached entries.
    /// </summary>
    int Count { get; }
}