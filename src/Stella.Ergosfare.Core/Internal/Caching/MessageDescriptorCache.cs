using System.Collections.Concurrent;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Caching;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Internal.Mediator;

namespace Stella.Ergosfare.Core.Internal.Caching;

/// <summary>
/// Cache for MessageDescriptor and MessageDependencies instances.
/// </summary>
internal sealed class MessageDescriptorCache(IDescriptorCacheStrategy strategy)
{
    private readonly IDescriptorCacheStrategy _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));

    /// <summary>
    /// Hot-path cache for resolved dependencies keyed by message type (no groups —
    /// the common case). Avoids building string keys on every dispatch.
    /// </summary>
    private readonly ConcurrentDictionary<Type, IMessageDependencies> _dependenciesByType = new();

    /// <summary>
    /// Hot-path cache for resolved dependencies keyed by message type and group set.
    /// </summary>
    private readonly ConcurrentDictionary<GroupedDependenciesKey, IMessageDependencies> _dependenciesByTypeAndGroups = new();

    /// <summary>
    /// Process-wide cache of provider-independent pipeline shapes (ordered, group-filtered
    /// descriptor arrays). Scoped dispatches materialize cheap lazy wrappers over these.
    /// </summary>
    private readonly ConcurrentDictionary<Type, MessagePipelineShape> _shapesByType = new();
    private readonly ConcurrentDictionary<GroupedDependenciesKey, MessagePipelineShape> _shapesByTypeAndGroups = new();

    private int _registryVersion = -1;

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

    public void Clear()
    {
        _strategy.Clear();
        _dependenciesByType.Clear();
        _dependenciesByTypeAndGroups.Clear();
        _shapesByType.Clear();
        _shapesByTypeAndGroups.Clear();
    }

    public void Evict(string key) => _strategy.Evict(key);

    public int Count => _strategy.Count;

    /// <summary>
    /// Drops all cached dependencies when the registry version changed, so runtime
    /// registrations (new messages or new handlers on existing messages) become visible.
    /// </summary>
    public void InvalidateIfRegistryChanged(int registryVersion)
    {
        if (Volatile.Read(ref _registryVersion) == registryVersion)
        {
            return;
        }

        _dependenciesByType.Clear();
        _dependenciesByTypeAndGroups.Clear();
        _shapesByType.Clear();
        _shapesByTypeAndGroups.Clear();
        Volatile.Write(ref _registryVersion, registryVersion);
    }

    /// <summary>
    /// Returns the cached provider-independent pipeline shape for the message type and
    /// group set, building it from the descriptor on first use.
    /// </summary>
    public MessagePipelineShape GetOrAddShape(Type messageType, string[] groups, IMessageDescriptor descriptor)
    {
        if (groups.Length == 0)
        {
            if (_shapesByType.TryGetValue(messageType, out var shape))
            {
                return shape;
            }

            return _shapesByType.GetOrAdd(messageType, MessagePipelineShape.Create(messageType, descriptor, groups));
        }

        var key = new GroupedDependenciesKey(messageType, groups);

        if (_shapesByTypeAndGroups.TryGetValue(key, out var groupedShape))
        {
            return groupedShape;
        }

        return _shapesByTypeAndGroups.GetOrAdd(key, MessagePipelineShape.Create(messageType, descriptor, groups));
    }

    public bool TryGetDependencies(Type messageType, string[] groups, out IMessageDependencies? dependencies)
    {
        return groups.Length == 0
            ? _dependenciesByType.TryGetValue(messageType, out dependencies)
            : _dependenciesByTypeAndGroups.TryGetValue(new GroupedDependenciesKey(messageType, groups), out dependencies);
    }

    public void AddDependencies(Type messageType, string[] groups, IMessageDependencies dependencies)
    {
        if (groups.Length == 0)
        {
            _dependenciesByType[messageType] = dependencies;
        }
        else
        {
            _dependenciesByTypeAndGroups[new GroupedDependenciesKey(messageType, groups)] = dependencies;
        }
    }

}
