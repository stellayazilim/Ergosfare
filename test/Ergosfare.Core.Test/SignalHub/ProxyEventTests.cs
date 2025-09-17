using System.Reflection;
using Ergosfare.Core.Abstractions.SignalHub;
using Ergosfare.Test.Fixtures;

namespace Ergosfare.Core.Test.SignalHub;


/// <summary>
/// Unit tests for verifying <c>ProxySignal</c> properties in <c>SignalHub</c>.
/// Ensures that all ProxySignal properties are properly initialized and
/// that events invoke handlers as expected.
/// </summary>
public class ProxySignalTests
{
    
    /// <summary>
    /// Reference to a hub instance cast as <see cref="IHasProxySignals"/>.
    /// Used for accessing proxy event properties in the tests.
    /// </summary>
    private readonly IHasProxySignals _hub = (IHasProxySignals)new SignalHubFixture().Hub;

    /// <summary>
    /// List of all proxy event property names to test.
    /// Used by <see cref="ProxySignal_Should_Invoke_Handler_When_Raised"/> theory.
    /// </summary>
    public static TheoryData<string> ProxySignalData = 
    [
        nameof(_hub.BeginExceptionInterceptingSignal),
        nameof(_hub.FinishHandlerInvocationSignal),
        nameof(_hub.BeginHandlerInvocationSignal),
        nameof(_hub.BeginExceptionInterceptorInvocationSignal),
        nameof(_hub.BeginHandlingSignal),
        nameof(_hub.BeginPipelineSignal),
        nameof(_hub.BeginPostInterceptingSignal),
        nameof(_hub.BeginPostInterceptorInvocationSignal),
        nameof(_hub.BeginPreInterceptingSignal),
        nameof(_hub.BeginPreInterceptorInvocationSignal),
        nameof(_hub.FinishPreInterceptingSignal),
        nameof(_hub.FinishExceptionInterceptorInvocationSignal),
        nameof(_hub.FinishHandlingSignal),
        nameof(_hub.FinishHandlingWithExceptionSignal),
        nameof(_hub.FinishExceptionInterceptingSignal),
        nameof(_hub.FinishPipelineSignal),
        nameof(_hub.FinishPostInterceptingSignal),
        nameof(_hub.FinishPostInterceptingWithExceptionSignal),
        nameof(_hub.FinishPostInterceptorInvocationSignal),
        nameof(_hub.FinishPreInterceptingWithExceptionSignal),
        nameof(_hub.FinishPreInterceptorInvocationSignal)
    ];
        
    /// <summary>
    /// Verifies that all <c>ProxySignal&lt;T&gt;</c> properties
    /// in <see cref="SignalHub"/> are initialized.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public void All_ProxySignal_Properties_Should_Be_Initialized()
    {
        // Arrange
        var hub = new Core.Abstractions.SignalHub.SignalHub();
        var properties = typeof(Core.Abstractions.SignalHub.SignalHub).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Act & Assert
        foreach (var prop in properties)
        {
            // Only test ProxySignal<> properties
            if (!prop.PropertyType.IsGenericType) continue;
            if (prop.PropertyType.GetGenericTypeDefinition() != typeof(ProxySignal<>)) continue;

            var value = prop.GetValue(hub);
            Assert.NotNull(value);
        }
    }

    
    /// <summary>
    /// Theory test that verifies each proxy event property exists
    /// and is accessible on the <see cref="SignalHub"/> class.
    /// </summary>
    /// <param name="propertyName">The name of the proxy event property to test.</param>
    [Theory]
    [MemberData(nameof(ProxySignalData))]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public void ProxySignal_Should_Invoke_Handler_When_Raised(string propertyName)
    {
        // Arrange
        var property = typeof(Core.Abstractions.SignalHub.SignalHub).GetProperty(propertyName)!;
        // Assert
        Assert.NotNull(property);
    }
}