using Ergosfare.Context;
using Ergosfare.Core.Internal.Contexts;
using Ergosfare.Core.Test.__stubs__;

namespace Ergosfare.Core.Test;

public class ExecutionContextTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ExecutionContextShouldConstructed()
    {
        var token = CancellationToken.None;
        var items = new Dictionary<object, object?>();
        // arrange & act
        var ctx = new ErgosfareExecutionContext(items, token);
        
        items.Add("foo", "bar");

        Assert.Throws<ExecutionAbortedException>(() => ctx.Abort("baz"));
        Assert.Equal(token, ctx.CancellationToken);
        Assert.Equal(items, ctx.Items);
        Assert.Equal("baz", ctx.MessageResult);
        
        
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NoExecutionContextExceptionShouldHaveDefaultMessage()
    {
        // arrange
        var exception = new NoExecutionContextException();
        
        // act & assert
        Assert.Equal("No execution context is set", exception.Message);
        
    }

}