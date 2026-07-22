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
    /// </summary>
    private readonly ConcurrentDictionary<Type, IMessageDependencies> _scopedDependenciesByType = new();
    private readonly ConcurrentDictionary<GroupedDependenciesKey, IMessageDependencies> _scopedDependenciesByTypeAndGroups = new();

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
                _scopedDependenciesByType.Clear();
                _scopedDependenciesByTypeAndGroups.Clear();
                _localRegistryVersion = registryVersion;
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

            var memoizedDependencies = new MessageDependencies(
                messageType, descriptor, _memoizedGraphProvider ?? _serviceProvider, groupsArray);
            cache.AddDependencies(messageType, groupsArray, memoizedDependencies);

            return memoizedDependencies;
        }

        if (groupsArray.Length == 0)
        {
            if (_scopedDependenciesByType.TryGetValue(messageType, out var scoped))
            {
                return scoped;
            }

            var dependencies = new MessageDependencies(messageType, descriptor, _serviceProvider, groupsArray);
            _scopedDependenciesByType[messageType] = dependencies;

            return dependencies;
        }

        var key = new GroupedDependenciesKey(messageType, groupsArray);

        if (_scopedDependenciesByTypeAndGroups.TryGetValue(key, out var scopedGrouped))
        {
            return scopedGrouped;
        }

        var groupedDependencies = new MessageDependencies(messageType, descriptor, _serviceProvider, groupsArray);
        _scopedDependenciesByTypeAndGroups[key] = groupedDependencies;

        return groupedDependencies;
    }
}
