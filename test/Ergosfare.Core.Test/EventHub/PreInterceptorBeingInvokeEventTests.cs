using Ergosfare.Core.Abstractions.Events;
using Hub = Ergosfare.Core.Abstractions.EventHub.EventHub;
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
            Message = "string",
            Result = null,
        };

        var interceptor2 = new BeginPipelineEvent()
        {
            Message = "string",
            Result = null,
        };

        var interceptor3 =  new BeginPipelineEvent()
        {
            Message = "string2",
            Result = null,
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