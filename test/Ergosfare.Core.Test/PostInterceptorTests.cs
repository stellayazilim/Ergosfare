using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Test.__stubs__;

namespace Ergosfare.Core.Test;

public class PostInterceptorTests
{
    
    private class TestIPostInterceptorTMessageTResultHandler: IPostInterceptor<IMessage, Task>
    {
        public object Handle(IMessage message, Task? messageResult, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
    
    private class TestIAsyncPostInterceptorTMessageTResultHandler: IAsyncPostInterceptor<IMessage, Task>
    {
    
        public Task HandleAsync(IMessage message, Task? messageResult, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }



    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public async Task TestPostInterceptorsShouldImplement()
    {
        // arrange 
        var ct = CancellationToken.None;
        var items = new Dictionary<object, object?>();
        var context = StubExecutionContext.Create();
        var msg = new StubNonGenericMessage();
        
        IPostInterceptor handler1 = new TestIPostInterceptorTMessageTResultHandler();
        IPostInterceptor<IMessage, Task> handler2 = new TestIAsyncPostInterceptorTMessageTResultHandler();
        
        
        await using (AmbientExecutionContext.CreateScope(context))
        {
            var result = handler1.Handle(msg,Task.CompletedTask, AmbientExecutionContext.Current); 
            Assert.NotNull(result);
            await Assert.IsType<Task>(result, exactMatch: false);
        }
        
        
        await using (AmbientExecutionContext.CreateScope(context))
        {
            var result = handler2.Handle(msg,Task.CompletedTask, AmbientExecutionContext.Current); 
            Assert.NotNull(result);
            await Assert.IsType<Task>(result, exactMatch: false);
        }
    }
}