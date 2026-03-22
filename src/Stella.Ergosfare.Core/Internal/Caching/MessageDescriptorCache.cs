using System.Collections.Concurrent;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Caching;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Caching;

/// <summary>
/// Cache for MessageDescriptor and MessageDependencies instances.
/// </summary>
internal sealed class MessageDescriptorCache
{
    private readonly IDescriptorCacheStrategy _strategy;

    public MessageDescriptorCache(IDescriptorCacheStrategy strategy)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
    }

    public bool TryGet<T>(string key, out T? value) where T : class
    {
        if (_strategy.TryGet(key, out var obj) && obj is T t)
        {
            value = t;
            return true;
        }
        
        value = null;
        return false;
    }

    public void Add(string key, IMessageDependencies value)
    {
        _strategy.Add(key, value);
    }

    public void Clear() => _strategy.Clear();
    
    public void Evict(string key) => _strategy.Evict(key);
    
    public int Count => _strategy.Count;
}