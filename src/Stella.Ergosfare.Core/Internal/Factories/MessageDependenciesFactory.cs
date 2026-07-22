using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Internal.Mediator;
using Stella.Ergosfare.Core.Internal.Caching;
using Stella.Ergosfare.Core.Internal.Registry;

namespace Stella.Ergosfare.Core.Internal.Factories;

public sealed class MessageDependenciesFactory : IMessageDependenciesFactory
{
    private readonly IServiceProvider _serviceProvider;
    private MessageDescriptorCache? _cache;
    private IMessageRegistry? _registry;
    private bool _registryResolved;

    public MessageDependenciesFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IMessageDependencies Create(Type messageType, IMessageDescriptor descriptor, IEnumerable<string> groups)
    {
        var cache = _cache ??= _serviceProvider.GetRequiredService<MessageDescriptorCache>();

        if (!_registryResolved)
        {
            _registry = _serviceProvider.GetService<IMessageRegistry>();
            _registryResolved = true;
        }

        if (_registry is not null)
        {
            // MessageRegistry.Version also changes when handlers are added to existing
            // messages; Count is the fallback for foreign registry implementations.
            var registryVersion = _registry is MessageRegistry registry ? registry.Version : _registry.Count;
            cache.InvalidateIfRegistryChanged(registryVersion);
        }

        var groupsArray = groups as string[] ?? groups.ToArray();

        if (cache.TryGetDependencies(messageType, groupsArray, out var dependencies))
        {
            return dependencies!;
        }

        dependencies = new MessageDependencies(messageType, descriptor, _serviceProvider, groupsArray);
        cache.AddDependencies(messageType, groupsArray, dependencies);

        return dependencies;
    }
}
