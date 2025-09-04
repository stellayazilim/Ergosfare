using Ergosfare.Core.Events;
using Hub = Ergosfare.Core.EventHub.EventHub;
namespace Ergosfare.Core.Test.EventHub;

public class PreInterceptorBeingInvokeEventTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldBeEqual()
    {
        // arrange 
        var now = DateTime.UtcNow;
        
     
        var interceptor1 = new BeginPipelineEvent()
        {
            MediatorInstance = typeof(string),
            ResultType = typeof(string),
            MessageType = typeof(string),
        };

        var interceptor2 = new BeginPipelineEvent()
        {
            MediatorInstance = typeof(string),
            ResultType = typeof(string),
            MessageType = typeof(string),
        };

        var interceptor3 =  new BeginPipelineEvent()
        {
            MediatorInstance = typeof(int),
            ResultType = typeof(string),
            MessageType = typeof(string),
        };
        // act

        var equality1 = interceptor1 == interceptor3;
        var equality2 = interceptor1.Equals(interceptor3);
        
        var equality3 = interceptor1 == interceptor2;
        var equality4 = interceptor1.Equals(interceptor2);
        
        // assert
        Assert.False(equality1);
        Assert.False(equality2);
        
        
        Assert.True(equality3);
        Assert.True(equality4);
    }
}