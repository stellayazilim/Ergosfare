using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Exceptions;

namespace Ergosfare.Core.Test.Common.Exceptions;

public class ExceptionTests
{
    [Fact]
    [Trait("Category", "Coverage")]
    public void InvalidMessageTypeExceptionTest()
    {
       var invalidMessageTypeException =  new InvalidMessageTypeException(typeof(IMessage));
       
       Assert.NotNull(invalidMessageTypeException);
       Assert.Equal($"Message of type {typeof(IMessage)} is not a valid message type.", invalidMessageTypeException.Message);
    }

    [Fact]
    [Trait("Category", "Coverage")]
    public void NoExecutionContextExceptionTest()
    {
        var noExecutionContextException = new NoExecutionContextException();
        
        Assert.NotNull(noExecutionContextException);
        Assert.Equal("No execution context is set", noExecutionContextException.Message);
    }


    [Fact]
    [Trait("Category", "Coverage")]
    public void MultipleHandlerExceptionTest()
    {
        // arrange & act
        var ex = new MultipleHandlerFoundException(typeof(IMessage), 2);
        
        // assert
        Assert.NotNull(ex);
        Assert.Equal("IMessage has 2 handlers registered.", ex.Message);
        Assert.True(ex.MessageType.IsAssignableTo(typeof(IMessage)));
        
    }
}