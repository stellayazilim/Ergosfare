using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Test.__stubs__;

namespace Ergosfare.Core.Test;

public class PostInterceptorTests
{
    
    private class TestIPostInterceptorTMessageTResultHandler: IAsyncPostInterceptor<IMessage, string>
    {
       
        public Task<object> HandleAsync(IMessage message, string? messageResult, IExecutionContext context)
        {
            return Task.FromResult<object>(messageResult ?? "string result");
        }
    }
    
    private class TestIAsyncPostInterceptorTMessageTResultHandler: IAsyncPostInterceptor<IMessage>
    {
        public Task<object> HandleAsync(IMessage message, object? _, IExecutionContext context)
        {
            return Task.FromResult(new object());
        }
    }



    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public async Task TestPostInterceptorsShouldImplement()
    {
        // arrange 
        var context = StubExecutionContext.Create();
        var msg = new StubNonGenericMessage();

        IPostInterceptor<IMessage, string> handler1 = new TestIPostInterceptorTMessageTResultHandler();
        IPostInterceptor<IMessage, object> handler2 = new TestIAsyncPostInterceptorTMessageTResultHandler();

        // Use a proper string result for handler1
        string initialStringResult = "test-result";

        await using (AmbientExecutionContext.CreateScope(context))
        {
            var result = handler1.Handle(msg, initialStringResult, AmbientExecutionContext.Current);
            Assert.NotNull(result);

            // Await the result if it is a Task<string>
            var awaitedResult = await Assert.IsType<Task<object>>(result, exactMatch: false);
            Assert.Equal(initialStringResult, awaitedResult);
        }

        await using (AmbientExecutionContext.CreateScope(context))
        {
            var result = handler2.Handle(msg, null, AmbientExecutionContext.Current);
            Assert.NotNull(result);

            // Await the Task returned by the async post interceptor
            await Assert.IsType<Task>(result, exactMatch: false);
        }
    }
}