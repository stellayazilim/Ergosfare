using Ergosfare.Core.Context;
using ExecutionContext = Ergosfare.Core.Internal.Contexts.ExecutionContext;

namespace Ergosfare.Core.Test;

public class ExecutionContextTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ExecutionContextShouldConstucted()
    {
        var token = CancellationToken.None;
        var items = new Dictionary<object, object?>();
        // arrange & act
        var ctx = new ExecutionContext(token, items);
        
        items.Add("foo", "bar");

        Assert.Throws<ExecutionAbortedException>(() => ctx.Abort("baz"));
        Assert.Equal(token, ctx.CancellationToken);
        Assert.Equal(items, ctx.Items);
        Assert.Equal("baz", ctx.MessageResult);
        
        
    }
}