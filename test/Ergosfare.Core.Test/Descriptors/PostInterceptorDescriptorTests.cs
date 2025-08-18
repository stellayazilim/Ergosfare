using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Test.__stubs__;

namespace Ergosfare.Core.Test;

public class PostInterceptorDescriptorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Integration")]
    public void MainHandlerDescriptorShouldGetResultType()
    {
        // arrange
        var descriptor = new PostInterceptorDescriptor()
        {
            MessageType = typeof(StubMessages.StubNonGenericMessage),
            HandlerType = typeof(StubHandlers.StubNonGenericPostInterceptor),
            ResultType = typeof(Task),
        };
        
        // act
        var resultType = descriptor.ResultType;
  
        // assert
        Assert.NotNull(resultType);
    }
}