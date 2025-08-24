using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Core.Internal.Contexts;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection.Test;

public class ModuleRegistryTests
{
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task ShouldInitialize()
    {

        // arrange
        var serviceCollection = new ServiceCollection();
        var messageRegistry = new MessageRegistry(new HandlerDescriptorBuilderFactory());

        await using var _ = AmbientExecutionContext.CreateScope(new ErgosfareExecutionContext(null, default));
        var moduleRegistry = new ModuleRegistry(serviceCollection, messageRegistry);
        
      
     
        // act
        
        moduleRegistry.Initialize();
        var serviceProvider = serviceCollection.BuildServiceProvider();
        
        // assert
        Assert.Same(serviceProvider.GetRequiredService<IMessageRegistry>(), messageRegistry);
        Assert.Same(AmbientExecutionContext.Current, serviceProvider.GetRequiredService<IExecutionContext>());
    }
}