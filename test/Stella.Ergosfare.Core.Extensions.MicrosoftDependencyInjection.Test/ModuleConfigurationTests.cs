
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection.Test;

/// <summary>
/// Contains unit tests for <see cref="ModuleConfiguration"/> and result adapter configuration,
/// verifying that services and adapters are correctly registered and resolved.
/// </summary>
public class ModuleConfigurationTests
{
    /// <summary>
    /// Tests that the <see cref="ModuleConfiguration"/> correctly exposes the provided <see cref="IServiceCollection"/>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldGetServiceProvider()
    {
        // arrange
        var serviceProvier = new ServiceCollection()
            .AddTransient<MessageHandler>();
        
        var moduleConfiguration = new ModuleConfiguration(serviceProvier, new FrozenCompositionCatalog());
        
        // act
        serviceProvier.BuildServiceProvider();
        
        // assert
        Assert.Same(serviceProvier, moduleConfiguration.Services);
    }
    
    
    
}