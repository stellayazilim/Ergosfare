using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Core.Internal.Contexts;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection.Test;

/// <summary>
/// Contains unit tests for <see cref="ModuleRegistry"/>,
/// verifying proper initialization and service registration.
/// </summary>
public class ModuleRegistryTests
{
    /// <summary>
    /// Tests that <see cref="ModuleRegistry"/> initializes correctly,
    /// registers services, and maintains the correct execution context.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldInitialize()
    {
        // arrange
        var serviceCollection = new ServiceCollection();
        var messageRegistry = new MessageRegistry(new HandlerDescriptorBuilderFactory());

        await using var _ = AmbientExecutionContext.CreateScope(new ErgosfareExecutionContext(null,null,  null, default));
        var moduleRegistry = new ModuleRegistry(serviceCollection, messageRegistry, new ResultAdapterService());
        // act
        moduleRegistry.Initialize();
        var serviceProvider = serviceCollection.BuildServiceProvider();
        // assert
        Assert.Same(serviceProvider.GetRequiredService<IMessageRegistry>(), messageRegistry);
        Assert.Same(AmbientExecutionContext.Current, serviceProvider.GetRequiredService<IExecutionContext>());
    }
}