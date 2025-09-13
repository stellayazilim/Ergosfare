using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Test.__stubs__;

namespace Ergosfare.Core.Test;

public class PreInterceptorTests
{
    
    private class TestIPreInterceptorTMessageHandler: IPreInterceptor<IMessage>
    {
        public object Handle(IMessage message, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
    
    
    private class TestIAsyncPreInterceptorTMessageHandler: IAsyncPreInterceptor<IMessage>
    {
        public Task<object> HandleAsync(IMessage message, IExecutionContext context)
        {
            return Task.FromResult<object>(message);
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
        
        IPreInterceptor handler1 = new TestIPreInterceptorTMessageHandler();
        IPreInterceptor<IMessage> handler2 = new TestIAsyncPreInterceptorTMessageHandler();
        
        await using (AmbientExecutionContext.CreateScope(context))
        {
            var result = handler1.Handle(msg, AmbientExecutionContext.Current); 
            Assert.NotNull(result);
            await Assert.IsType<Task>(result, exactMatch: false);
        }
        
        await using (AmbientExecutionContext.CreateScope(context))
        {
            var result = handler2.Handle(msg, AmbientExecutionContext.Current); 
            Assert.NotNull(result);
            await Assert.IsType<Task>(result, exactMatch: false);
        }
    }
}