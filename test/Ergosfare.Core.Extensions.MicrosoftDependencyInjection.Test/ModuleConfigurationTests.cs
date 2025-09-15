using System.Reflection;
using Ergosfare.Core.Abstractions;
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

    private class TestAdapter: IResultAdapter
    {
        public bool CanAdapt(object result)
        {
            return true;
        }

        public bool TryGetException(object result, out Exception? exception)
        {
            exception = new Exception("Hello world");
            return true;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShoulldAddMessageAdapter()
    {
        var serviceProvier = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.ConfigureResultAdapters(adapter => adapter.Register<TestAdapter>())
                    .AddCoreModule( b => {});
            })
            .BuildServiceProvider();

        var resultAdapterService = serviceProvier.GetService<IResultAdapterService>();
        
        var exception = resultAdapterService?.LookupException("hello world");
        Assert.NotNull(resultAdapterService);
        Assert.Equal(new Exception("Hello world").Message, exception?.Message);
        Assert.Single(resultAdapterService.GetAdapters());
    }
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShoulldAddMessageAdapterFromAssembly()
    {
        var serviceProvier = new ServiceCollection()
            .AddErgosfare(options => options
                    .ConfigureResultAdapters(adapter => adapter.RegisterFromAssembly(Assembly.GetExecutingAssembly()))
                    .AddCoreModule( b => {}))
            .BuildServiceProvider();

        var resultAdapterService = serviceProvier.GetService<IResultAdapterService>();
        

        Assert.IsType<TestAdapter>(resultAdapterService?.GetAdapters().FirstOrDefault());
    }
}