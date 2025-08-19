using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Test.__stubs__;

namespace Ergosfare.Core.Test;

public class ExceptionInterceptorDescriptorTests
{


    [Fact]
    [Trait("Category", "Unit")]
    public void ExceptionInterceptorShouldGetResultType()
    {
        // arrange
        var descriptor = new ExceptionInterceptorDescriptor()
        {
            MessageType = typeof(StubMessages.StubNonGenericMessage),
            HandlerType = typeof(StubHandlers.StubNonGenericExceptionInterceptor),
            ResultType = typeof(Task)
        };
        
        // act
        var resultType = descriptor.ResultType;
        
        // assert
        Assert.NotNull(resultType);
    }
}