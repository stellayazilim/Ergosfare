using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Internal.Mediator;
using Stella.Ergosfare.Core.Internal.Caching;
using Stella.Ergosfare.Core.Internal.Registry;

namespace Stella.Ergosfare.Core.Internal.Factories;

/// <summary>
/// Creates (and caches) the resolved handler graph for a message type.
/// </summary>
/// <remarks>
/// Two caching modes exist, chosen per message:
/// <list type="bullet">
/// <item>Messages whose handlers and interceptors are all singleton-registered (or when
/// <see cref="ErgosfareRuntimeOptions.MemoizeAllHandlers"/> is enabled) are memoized
/// process-wide, bound to the root provider — the fast path.</item>
/// <item>Everything else is bound to the calling scope's provider and cached per factory
/// instance. The factory is registered scoped, so scoped and transient handler
/// dependencies are honored per scope, and disposables are disposed with the scope.</item>
/// </list>
/// </remarks>
public sealed class MessageDependenciesFactory : IMessageDependenciesFactory
{
    private readonly IServiceProvider _serviceProvider;

    private MessageDescriptorCache? _cache;
    private IMessageRegistry? _registry;
    private HandlerLifetimeRegistry? _handlerLifetimes;
    private ErgosfareRuntimeOptions? _runtimeOptions;
    private IServiceProvider? _memoizedGraphProvider;
    private bool _servicesResolved;
    private int _localRegistryVersion = -1;

    /// <summary>
    /// Per-factory (per-scope) cache of dependencies bound to this factory's provider.
    /// Scopes are short-lived and rarely contended, so a plain dictionary under a lock
    /// keeps the per-scope allocation footprint small.
    /// </summary>
    private readonly object _scopedCacheLock = new();
    private Dictionary<Type, IMessageDependencies>? _scopedDependenciesByType;
    private Dictionary<GroupedDependenciesKey, IMessageDependencies>? _scopedDependenciesByTypeAndGroups;

    public MessageDependenciesFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IMessageDependencies Create(Type messageType, IMessageDescriptor descriptor, IEnumerable<string> groups)
    {
        var cache = _cache ??= _serviceProvider.GetRequiredService<MessageDescriptorCache>();

        if (!_servicesResolved)
        {
            _registry = _serviceProvider.GetService<IMessageRegistry>();
            _handlerLifetimes = _serviceProvider.GetService<HandlerLifetimeRegistry>();
            _runtimeOptions = _serviceProvider.GetService<ErgosfareRuntimeOptions>();
            _memoizedGraphProvider = _serviceProvider.GetService<RootServiceProviderAccessor>()?.RootProvider ?? _serviceProvider;
            _servicesResolved = true;
        }

        if (_registry is not null)
        {
            // MessageRegistry.Version also changes when handlers are added to existing
            // messages; Count is the fallback for foreign registry implementations.
            var registryVersion = _registry is MessageRegistry registry ? registry.Version : _registry.Count;
            cache.InvalidateIfRegistryChanged(registryVersion);
            _handlerLifetimes?.InvalidateIfRegistryChanged(registryVersion);

            if (_localRegistryVersion != registryVersion)
            {
                lock (_scopedCacheLock)
                {
                    _scopedDependenciesByType = null;
                    _scopedDependenciesByTypeAndGroups = null;
                    _localRegistryVersion = registryVersion;
                }
            }
        }

        var groupsArray = groups as string[] ?? groups.ToArray();

        var memoizeGraph = (_runtimeOptions?.MemoizeAllHandlers ?? false)
                           || (_handlerLifetimes?.AreAllHandlersSingleton(messageType, descriptor) ?? false);

        if (memoizeGraph)
        {
            if (cache.TryGetDependencies(messageType, groupsArray, out var memoized))
            {
                return memoized!;
            }

            // The memoized graph shares the same process-wide shape cache as the scoped
            // path, so type resolution work is done once regardless of caching mode.
            var memoizedShape = cache.GetOrAddShape(messageType, groupsArray, descriptor);
            var memoizedDependencies = new MessageDependencies(
                memoizedShape, _memoizedGraphProvider ?? _serviceProvider);
            cache.AddDependencies(messageType, groupsArray, memoizedDependencies);

            return memoizedDependencies;
        }

        if (groupsArray.Length == 0)
        {
            lock (_scopedCacheLock)
            {
                if (_scopedDependenciesByType?.TryGetValue(messageType, out var scoped) == true)
                {
                    return scoped;
                }

                // The heavy part (ordering/filtering) comes from the process-wide shape
                // cache; only cheap lazy wrappers are materialized per scope. Building
                // runs no user code (handlers stay lazy), so it happens inside the lock.
                var shape = cache.GetOrAddShape(messageType, groupsArray, descriptor);
                var dependencies = new MessageDependencies(shape, _serviceProvider);
                (_scopedDependenciesByType ??= new Dictionary<Type, IMessageDependencies>())[messageType] = dependencies;

                return dependencies;
            }
        }

        var key = new GroupedDependenciesKey(messageType, groupsArray);

        lock (_scopedCacheLock)
        {
            if (_scopedDependenciesByTypeAndGroups?.TryGetValue(key, out var scopedGrouped) == true)
            {
                return scopedGrouped;
            }

            var groupedShape = cache.GetOrAddShape(messageType, groupsArray, descriptor);
            var groupedDependencies = new MessageDependencies(groupedShape, _serviceProvider);
            (_scopedDependenciesByTypeAndGroups ??= new Dictionary<GroupedDependenciesKey, IMessageDependencies>())[key] = groupedDependencies;

            return groupedDependencies;
        }
    }
}
