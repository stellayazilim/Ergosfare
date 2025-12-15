using System.Reflection;
using Stella.Ergosfare.Core.Abstractions;
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
        
        var moduleConfiguration = new ModuleConfiguration(serviceProvier, null);
        
        // act
        serviceProvier.BuildServiceProvider();
        
        // assert
        Assert.Same(serviceProvier, moduleConfiguration.Services);
    }
    
    /// <summary>
    /// A test implementation of <see cref="IResultAdapter"/> used for verifying result adapter registration.
    /// </summary>
    private class TestAdapter: IResultAdapter
    {
        /// <summary>
        /// Determines whether this adapter can adapt the provided result.
        /// </summary>
        /// <param name="result">The result to check.</param>
        /// <returns>Always returns <c>true</c>.</returns>
        public bool CanAdapt(object result)
        {
            return true;
        }

        /// <summary>
        /// Attempts to extract an exception from the provided result.
        /// </summary>
        /// <param name="result">The result to extract from.</param>
        /// <param name="exception">Outputs the extracted exception.</param>
        /// <returns>Always returns <c>true</c> with a sample exception.</returns>
        public bool TryGetException(object result, out Exception? exception)
        {
            exception = new Exception("Hello world");
            return true;
        }
    }
    
    /// <summary>
    /// Tests that a custom result adapter can be registered and retrieved from the <see cref="IResultAdapterService"/>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldAddMessageAdapter()
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
    
    /// <summary>
    /// Tests that result adapters can be registered from an assembly and retrieved correctly.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldAddMessageAdapterFromAssembly()
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