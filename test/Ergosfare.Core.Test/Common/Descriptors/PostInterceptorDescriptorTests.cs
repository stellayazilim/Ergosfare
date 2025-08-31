using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Core.Test.__stubs__;

namespace Ergosfare.Core.Test.Common;
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
            Weight = 1,
            Groups = [],
            MessageType = typeof(StubNonGenericMessage),
            HandlerType = typeof(StubNonGenericPostInterceptor),
            ResultType = typeof(Task),
        };
        
        // act
        var resultType = descriptor.ResultType;
  
        // assert
        Assert.NotNull(resultType);
    }
}