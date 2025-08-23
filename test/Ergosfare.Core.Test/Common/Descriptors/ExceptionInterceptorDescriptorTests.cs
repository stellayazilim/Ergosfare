using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Core.Test.__stubs__;

namespace Ergosfare.Core.Test.Common;

public class ExceptionInterceptorDescriptorTests
{


    [Fact]
    [Trait("Category", "Unit")]
    public void ExceptionInterceptorShouldGetResultType()
    {
        // arrange
        var descriptor = new ExceptionInterceptorDescriptor()
        {
            MessageType = typeof(StubNonGenericMessage),
            HandlerType = typeof(StubNonGenericExceptionInterceptor),
            ResultType = typeof(object)
        };
        
        // act
        var resultType = descriptor.ResultType;
        
        // assert
        Assert.NotNull(resultType);
    }
}