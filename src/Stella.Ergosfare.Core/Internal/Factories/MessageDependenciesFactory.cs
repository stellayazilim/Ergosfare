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
/// Creates (and caches process-wide) the resolved handler graph for a message type.
/// </summary>
/// <remarks>
/// Dependencies are provider-independent: handler instances are resolved per invocation
/// from the dispatching scope's provider (carried by the execution context), so DI
/// lifetimes are honored without binding the graph to any scope. Messages whose pipeline
/// is fully singleton-registered — or all messages when
/// <see cref="ErgosfareRuntimeOptions.MemoizeAllHandlers"/> is enabled — additionally
/// cache resolved instances inside their references, pinned to the root provider.
/// The factory itself is registered as a singleton; every dispatch after the first is a
/// cache lookup.
/// </remarks>
internal sealed class MessageDependenciesFactory(IServiceProvider serviceProvider) : IMessageDependenciesFactory
{
    private MessageDescriptorCache? _cache;
    private IMessageRegistry? _registry;
    private HandlerLifetimeRegistry? _handlerLifetimes;
    private ErgosfareRuntimeOptions? _runtimeOptions;
    private IServiceProvider? _memoizedGraphProvider;
    private bool _servicesResolved;

    public IMessageDependencies Create(Type messageType, IMessageDescriptor descriptor, IEnumerable<string> groups)
    {
        var cache = _cache ??= serviceProvider.GetRequiredService<MessageDescriptorCache>();

        if (!_servicesResolved)
        {
            _registry = serviceProvider.GetService<IMessageRegistry>();
            _handlerLifetimes = serviceProvider.GetService<HandlerLifetimeRegistry>();
            _runtimeOptions = serviceProvider.GetService<ErgosfareRuntimeOptions>();
            _memoizedGraphProvider = serviceProvider.GetService<RootServiceProviderAccessor>()?.RootProvider ?? serviceProvider;
            _servicesResolved = true;
        }

        if (_registry is not null)
        {
            // MessageRegistry.Version also changes when handlers are added to existing
            // messages; Count is the fallback for foreign registry implementations.
            var registryVersion = _registry is MessageRegistry registry ? registry.Version : _registry.Count;
            cache.InvalidateIfRegistryChanged(registryVersion);
            _handlerLifetimes?.InvalidateIfRegistryChanged(registryVersion);
        }

        var groupsArray = groups as string[] ?? groups.ToArray();

        if (cache.TryGetDependencies(messageType, groupsArray, out var cached))
        {
            return cached!;
        }

        var shape = cache.GetOrAddShape(messageType, groupsArray, descriptor);

        // Pipelines that are fully singleton-registered (or forced via MemoizeAllHandlers)
        // cache handler instances inside their references, pinned to the root provider.
        var memoizeInstances = (_runtimeOptions?.MemoizeAllHandlers ?? false)
                               || (_handlerLifetimes?.AreAllHandlersSingleton(messageType, descriptor) ?? false);

        var dependencies = new MessageDependencies(
            shape, memoizeInstances ? _memoizedGraphProvider ?? serviceProvider : null);

        cache.AddDependencies(messageType, groupsArray, dependencies);

        return dependencies;
    }
}
