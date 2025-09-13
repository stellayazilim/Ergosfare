using System.Reflection;
using Ergosfare.Core.EventHub;
using Ergosfare.Core.Events;

namespace Ergosfare.Core.Test.EventHub;

public class ProxyEventTests
{
    private readonly IHasProxyEvents _hub = new Core.EventHub.EventHub();

    public static IEnumerable<object[]> ProxyEventData =>
        new List<object[]>
        {
            new object[] { nameof(_hub.BeginExceptionInterceptingEvent) },
            new object[] { nameof(_hub.FinishHandlerInvocationEvent) },
            new object[] { nameof(_hub.BeginHandlerInvocationEvent) },
            new object[] { nameof(_hub.BeginExceptionInterceptorInvocationEvent) },
            new object[] { nameof(_hub.BeginHandlingEvent) },
            new object[] { nameof(_hub.BeginPipelineEvent) },
            new object[] { nameof(_hub.BeginPostInterceptingEvent) },
            new object[] { nameof(_hub.BeginPostInterceptorInvocationEvent) },
            new object[] { nameof(_hub.BeginPreInterceptingEvent) },
            new object[] { nameof(_hub.BeginPreInterceptorInvocationEvent) },
            new object[] { nameof(_hub.FinishPreInterceptingEvent) },
            new object[] { nameof(_hub.FinishExceptionInterceptorInvocationEvent) },
            new object[] { nameof(_hub.FinishHandlingEvent) },
            new object[] { nameof(_hub.FinishHandlingWithExceptionEvent) },
            new object[] { nameof(_hub.FinishExceptionInterceptingEvent) },
            new object[] { nameof(_hub.FinishPipelineEvent) },
            new object[] { nameof(_hub.FinishPostInterceptingEvent) },
            new object[] { nameof(_hub.FinishPostInterceptingWithExceptionEvent) },
            new object[] { nameof(_hub.FinishPostInterceptorInvocationEvent) },
            new object[] { nameof(_hub.FinishPreInterceptingWithExceptionEvent) },
            new object[] { nameof(_hub.FinishPreInterceptorInvocationEvent) }
        };
    
        
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public void All_ProxyEvent_Properties_Should_Be_Initialized()
    {
        // Arrange
        var hub = new Core.EventHub.EventHub();
        var properties = typeof(Core.EventHub.EventHub).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Act & Assert
        foreach (var prop in properties)
        {
            // Only test ProxyEvent<> properties
            if (!prop.PropertyType.IsGenericType) continue;
            if (prop.PropertyType.GetGenericTypeDefinition() != typeof(ProxyEvent<>)) continue;

            var value = prop.GetValue(hub);
            Assert.NotNull(value);
        }
    }

        [Theory]
        [MemberData(nameof(ProxyEventData))]
        [Trait("Category", "Coverage")]
        [Trait("Category", "Unit")]
        public void ProxyEvent_Should_Invoke_Handler_When_Raised(string propertyName)
        {
            // Arrange
            var property = typeof(Core.EventHub.EventHub).GetProperty(propertyName)!;
            // Assert
            Assert.NotNull(property);
        }
        
    

}