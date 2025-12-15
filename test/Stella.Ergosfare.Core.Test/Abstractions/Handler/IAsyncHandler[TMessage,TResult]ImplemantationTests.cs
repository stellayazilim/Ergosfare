using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Test.Fixtures;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Stella.Ergosfare.Core.Test.Abstractions.Handler;


/// <summary>
/// Unit tests to verify that <see cref="IAsyncHandler{TMessage, TResult}"/> 
/// implementations behave correctly when invoked through the non-generic 
/// <see cref="IHandler"/> interface.
/// </summary>
/// <remarks>
/// These tests ensure that asynchronous handlers returning a result propagate
/// their result type correctly when called via the non-generic interface.
/// </remarks>
public class IAsyncHandlerTMessageTResultTests(ExecutionContextFixture executionContextFixture)
    : BaseHandlerFixture(executionContextFixture)
{
    
    /// <summary>
    /// Verifies that a handler implementing 
    /// <see cref="IAsyncHandler{TMessage, TResult}"/> returns a 
    /// <see cref="Task{TResult}"/> when invoked via the non-generic 
    /// <see cref="IHandler"/> interface.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task IAsyncHandlersShouldImplements()
    {
        var ctx = ExecutionContextFixture.Ctx;
        IHandler handler = new StubStringAsyncHandler();
        
        var result = handler.Handle(Message, ctx);
        
        Assert.NotNull(result);
        await Assert.IsType<Task<string>>(result);

    }
}