using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection.Test;

public class ModuleConfigurationTests
{


    [Fact]
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