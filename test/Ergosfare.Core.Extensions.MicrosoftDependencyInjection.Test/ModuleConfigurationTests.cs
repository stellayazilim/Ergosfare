using Microsoft.Extensions.DependencyInjection;
using Xunit.Sdk;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection.Test;


public class ModuleConfigurationTests
{


    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldGetServiceProvider()
    {
        // arrange
        var serviceProvier = new ServiceCollection()
            .AddTransient<MessageHandler>();
        
        var moduleConfiguration = new ModuleConfiguration(serviceProvier, null);
        
        // act
        serviceProvier.BuildServiceProvider();
        
        // assert
        Assert.Same(serviceProvier, moduleConfiguration.Services);
    }
}