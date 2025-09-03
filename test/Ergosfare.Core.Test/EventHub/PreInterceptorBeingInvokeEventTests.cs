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
        
     
        var interceptor1 = new Hub.PreInterceptorBeingInvokeEvent()
        {
            Timestamp = now,
            InterceptorName = "TestInterceptor",
        };

        var interceptor2 = new Hub.PreInterceptorBeingInvokeEvent()
        {
            Timestamp = now,
            InterceptorName = "TestInterceptor2",
        };

        var interceptor3 = new Hub.PreInterceptorBeingInvokeEvent()
        {
            Timestamp = now,
            InterceptorName = "TestInterceptor",
        };


        var interceptor4 = new object();
        // act

        var equality1 = interceptor1 == interceptor2;
        var equality2 = interceptor1.Equals(interceptor2);
        
        var equality3 = interceptor1 == interceptor3;
        var equality4 = interceptor1.Equals(interceptor3);
        
        var equality5 = interceptor1.Equals(interceptor4);
        
        // assert
        Assert.False(equality1);
        Assert.False(equality2);
        Assert.False(equality5);
        
        
        Assert.True(equality3);
        Assert.True(equality4);
    }
}