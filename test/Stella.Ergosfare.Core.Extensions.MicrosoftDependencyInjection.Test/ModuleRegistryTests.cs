using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Internal.Factories;
using Stella.Ergosfare.Core.Internal.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection.Test;

/// <summary>
/// Contains unit tests for <see cref="ModuleRegistry"/>,
/// verifying proper initialization and service registration.
/// </summary>
public class ModuleRegistryTests
{
    /// <summary>
    /// Tests that <see cref="ModuleRegistry"/> initializes correctly
    /// and registers services.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldInitialize()
    {
        // arrange
        var serviceCollection = new ServiceCollection();
        var messageRegistry = new MessageRegistry(new HandlerDescriptorBuilderFactory());

        var moduleRegistry = new ModuleRegistry(serviceCollection, messageRegistry, new ResultAdapterService());
        // act
        moduleRegistry.Initialize();
        var serviceProvider = serviceCollection.BuildServiceProvider();
        // assert
        Assert.Same(serviceProvider.GetRequiredService<IMessageRegistry>(), messageRegistry);
    }
}