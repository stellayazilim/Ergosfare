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

    /// <summary>
    /// Hot-path cache for resolved dependencies keyed by message type (no groups —
    /// the common case). Avoids building string keys on every dispatch.
    /// </summary>
    private readonly ConcurrentDictionary<Type, IMessageDependencies> _dependenciesByType = new();

    /// <summary>
    /// Hot-path cache for resolved dependencies keyed by message type and group set.
    /// </summary>
    private readonly ConcurrentDictionary<GroupedDependenciesKey, IMessageDependencies> _dependenciesByTypeAndGroups = new();

    private int _registryVersion = -1;

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

    public void Clear()
    {
        _strategy.Clear();
        _dependenciesByType.Clear();
        _dependenciesByTypeAndGroups.Clear();
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
        Volatile.Write(ref _registryVersion, registryVersion);
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

    /// <summary>
    /// Allocation-free composite key: message type plus ordered group names,
    /// compared with ordinal sequence equality.
    /// </summary>
    private readonly struct GroupedDependenciesKey(Type messageType, string[] groups) : IEquatable<GroupedDependenciesKey>
    {
        private readonly Type _messageType = messageType;
        private readonly string[] _groups = groups;

        public bool Equals(GroupedDependenciesKey other)
        {
            if (_messageType != other._messageType || _groups.Length != other._groups.Length)
            {
                return false;
            }

            for (var i = 0; i < _groups.Length; i++)
            {
                if (!string.Equals(_groups[i], other._groups[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is GroupedDependenciesKey other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(_messageType);

            foreach (var group in _groups)
            {
                hash.Add(group, StringComparer.Ordinal);
            }

            return hash.ToHashCode();
        }
    }
}
