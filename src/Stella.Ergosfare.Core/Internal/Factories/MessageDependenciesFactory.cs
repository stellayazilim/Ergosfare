using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Internal.Mediator;
using Stella.Ergosfare.Core.Internal.Caching;

namespace Stella.Ergosfare.Core.Internal.Factories;

public sealed class MessageDependenciesFactory : IMessageDependenciesFactory
{
    private readonly IServiceProvider _serviceProvider;

    public MessageDependenciesFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IMessageDependencies Create(Type messageType, IMessageDescriptor descriptor, IEnumerable<string> groups)
    {
        var groupsArray = groups as string[] ?? groups.ToArray();
        var groupsKey = groupsArray.Length == 0 ? "" : string.Join('|', groupsArray);
        var cacheKey = $"{messageType.FullName}|{groupsKey}";
        
        var cache = _serviceProvider.GetRequiredService<MessageDescriptorCache>();
        
        // Cache'te var mı?
        if (cache.TryGet<IMessageDependencies>(cacheKey, out var dependencies))
        {
            return dependencies;
        }
        
        // Yoksa oluştur ve cache'e ekle
        dependencies = new MessageDependencies(messageType, descriptor, _serviceProvider, groupsArray);
        cache.Add(cacheKey, dependencies);
        
        return dependencies;
    }
}