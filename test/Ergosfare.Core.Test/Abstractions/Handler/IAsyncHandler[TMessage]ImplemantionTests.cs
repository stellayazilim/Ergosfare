using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Test.Fixtures;
using Ergosfare.Test.Fixtures.Stubs.Basic;
// ReSharper disable once ConvertToPrimaryConstructor
namespace Ergosfare.Core.Test.Abstractions.Handler;


/// <summary>
/// Unit tests for asynchronous handlers using basic stubs and <see cref="ExecutionContextFixture"/>.
/// Demonstrates that <see cref="IAsyncHandler{TMessage}"/> implementations produce <see cref="Task"/> results.
/// </summary>
public class AsyncHandlerTests(ExecutionContextFixture executionContextFixture) 
    :BaseHandlerFixture(executionContextFixture)
{

    
    /// <summary>
    /// Verifies that an <see cref="IAsyncHandler{TMessage}"/> correctly implements the async contract.
    /// Specifically, <see cref="StubVoidAsyncHandler"/> should return a <see cref="Task"/>.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task IAsyncHandlersShouldImplement()
    {      
        // arrange
        IHandler handler1 =  new StubVoidAsyncHandler();
       
        // act
        var result = handler1.Handle(Message, ExecutionContextFixture.Ctx);
        
        // assert
        Assert.NotNull(result); 
        await Assert.IsType<Task>(result, exactMatch: false);
    }
}