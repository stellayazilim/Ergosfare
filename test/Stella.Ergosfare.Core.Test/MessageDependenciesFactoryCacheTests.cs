using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Internal.Caching;
using Stella.Ergosfare.Core.Internal.Mediator;
using Stella.Ergosfare.Core.Internal.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Test;

public class SimpleCacheTest
{
    record TestMessage : IMessage;
    
    [Fact]
    public void Cache_AddAndTryGet_ShouldWork()
    {

        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var messageDescriptor = new MessageDescriptor(typeof(TestMessage));
        var messageDependencies =  new MessageDependencies(typeof(TestMessage), messageDescriptor, serviceProvider, []);

        var cache = new MessageDescriptorCache(new LruCacheStrategy());
        
        cache.Add(nameof(TestMessage), messageDependencies);
        
        IMessageDependencies? dependencies;
        cache.TryGet(nameof(TestMessage), out dependencies);
        
        
        Assert.NotNull(dependencies);
        Assert.Equal(messageDependencies, dependencies);
    }
}