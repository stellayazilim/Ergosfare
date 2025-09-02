using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Core.Test.__factories__;

internal class SingleMessageDependencyMediationFactory
{
    public static (IMessageDependencies dependencies, IMessageDescriptor? descriptor, MessageRegistry registry) Create<TMessage>(
            params Type[] types)
    {
        var serviceCollection = new ServiceCollection();

        foreach (var type in types)
        {
            serviceCollection.AddTransient(type);
        }

        var serviceProvider = serviceCollection.BuildServiceProvider();

        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());

        foreach (var type in types)
        {
            registry.Register(type);
        }
        
        // 3. Resolve descriptor
        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry);
        var descriptor = resolver.Find(typeof(TMessage));
        
        // 4. Create dependencies
        var dependencyFactory = new MessageDependenciesFactory(serviceProvider);
        var dependencies = dependencyFactory.Create(typeof(TMessage), descriptor!, []);
        
        return (dependencies,  descriptor, registry);
    }
}