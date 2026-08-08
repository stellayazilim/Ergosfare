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
    /// A cached value stamped with the registry version its build STARTED at. The stamp
    /// closes the invalidate/add race: a build that began before a registration completes
    /// may finish (and store) after the invalidation clear, but it carries the old
    /// version, so readers at the new version miss it and rebuild instead of serving a
    /// stale pipeline until the next (possibly never-coming) version bump.
    /// </summary>
    private readonly record struct VersionedEntry<T>(int Version, T Value);

    /// <summary>
    /// Hot-path cache for resolved dependencies keyed by message type (no groups —
    /// the common case). Avoids building string keys on every dispatch.
    /// </summary>
    private readonly ConcurrentDictionary<Type, VersionedEntry<IMessageDependencies>> _dependenciesByType = new();

    /// <summary>
    /// Hot-path cache for resolved dependencies keyed by message type and group set.
    /// </summary>
    private readonly ConcurrentDictionary<GroupedDependenciesKey, VersionedEntry<IMessageDependencies>> _dependenciesByTypeAndGroups = new();

    /// <summary>
    /// Process-wide cache of provider-independent pipeline shapes (ordered, group-filtered
    /// descriptor arrays). Scoped dispatches materialize cheap lazy wrappers over these.
    /// </summary>
    private readonly ConcurrentDictionary<Type, VersionedEntry<MessagePipelineShape>> _shapesByType = new();
    private readonly ConcurrentDictionary<GroupedDependenciesKey, VersionedEntry<MessagePipelineShape>> _shapesByTypeAndGroups = new();

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
    /// group set, building it from the descriptor on first use. A cached shape counts
    /// only when it was built at the given registry version — a stale entry (a build that
    /// raced a registration) is rebuilt and overwritten in place.
    /// </summary>
    public MessagePipelineShape GetOrAddShape(Type messageType, string[] groups, IMessageDescriptor descriptor, int registryVersion)
    {
        if (groups.Length == 0)
        {
            if (_shapesByType.TryGetValue(messageType, out var entry) && entry.Version == registryVersion)
            {
                return entry.Value;
            }

            var shape = MessagePipelineShape.Create(messageType, descriptor, groups);
            _shapesByType[messageType] = new VersionedEntry<MessagePipelineShape>(registryVersion, shape);
            return shape;
        }

        var key = new GroupedDependenciesKey(messageType, groups);

        if (_shapesByTypeAndGroups.TryGetValue(key, out var groupedEntry) && groupedEntry.Version == registryVersion)
        {
            return groupedEntry.Value;
        }

        var groupedShape = MessagePipelineShape.Create(messageType, descriptor, groups);
        _shapesByTypeAndGroups[key] = new VersionedEntry<MessagePipelineShape>(registryVersion, groupedShape);
        return groupedShape;
    }

    /// <summary>
    /// A cached entry counts only when it was built at the given registry version; see
    /// <see cref="VersionedEntry{T}"/> for why a bare presence check is not enough.
    /// </summary>
    public bool TryGetDependencies(Type messageType, string[] groups, int registryVersion, out IMessageDependencies? dependencies)
    {
        var found = groups.Length == 0
            ? _dependenciesByType.TryGetValue(messageType, out var entry)
            : _dependenciesByTypeAndGroups.TryGetValue(new GroupedDependenciesKey(messageType, groups), out entry);

        if (found && entry.Version == registryVersion)
        {
            dependencies = entry.Value;
            return true;
        }

        dependencies = null;
        return false;
    }

    public void AddDependencies(Type messageType, string[] groups, IMessageDependencies dependencies, int registryVersion)
    {
        var entry = new VersionedEntry<IMessageDependencies>(registryVersion, dependencies);

        if (groups.Length == 0)
        {
            _dependenciesByType[messageType] = entry;
        }
        else
        {
            _dependenciesByTypeAndGroups[new GroupedDependenciesKey(messageType, groups)] = entry;
        }
    }

}
