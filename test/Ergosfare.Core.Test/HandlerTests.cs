

using System.Runtime.CompilerServices;
using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Test.__stubs__;
using ExecutionContext = Ergosfare.Core.Internal.Contexts.ExecutionContext;

namespace Ergosfare.Core.Test;

public class HandlerTests
{
    private class TestIAsyncHandlerTMessage: IAsyncHandler<IMessage>
    {
        public Task HandleAsync(IMessage message, IExecutionContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
    
    
    private class TestIAsyncHandlerTMessageTResult:IAsyncHandler<IMessage, string>
    {

        public Task<string> HandleAsync(IMessage message, IExecutionContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(string.Empty);
        }
    }
    
    
    private class TestIStreamHandlerTMessageTResult: IStreamHandler<IMessage, string>
    {
        public async IAsyncEnumerable<string> StreamAsync(IMessage message, IExecutionContext context,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return  await Task.FromResult(string.Empty);
        }
    }

    [Fact]
    [Trait("Category", "Coverage")]
    public async Task IHandlersShouldImplement()
    {
        
        var ct = CancellationToken.None;
        var values = new Dictionary<object, object?>();
        var ctx = new ExecutionContext(ct, values);
        IHandler<IMessage, Task> handler1 =  new TestIAsyncHandlerTMessage();
        IHandler<IMessage, Task<string>> handler2 =  new TestIAsyncHandlerTMessageTResult();
        IHandler<IMessage, IAsyncEnumerable<string>> handler3 =  new TestIStreamHandlerTMessageTResult();
       
        await using (AmbientExecutionContext.CreateScope(ctx))
        {

            var result = handler1.Handle(
                new StubMessages.StubNonGenericMessage(), AmbientExecutionContext.Current);
            Assert.NotNull(result); 
            await Assert.IsType<Task>(result, exactMatch: false);
            
        }
        
        
        await using (AmbientExecutionContext.CreateScope(ctx))
        {

            var result = handler2.Handle(
                new StubMessages.StubNonGenericMessage(), AmbientExecutionContext.Current);
            Assert.NotNull(result); 
            
            
            await Assert.IsType<Task<string>>(result, exactMatch: false);
            
        }
        
        
               
        await using (AmbientExecutionContext.CreateScope(ctx))
        {

            await foreach (var item in handler3.Handle(new StubMessages.StubNonGenericMessage(), AmbientExecutionContext.Current))
            {
                Assert.Equal(string.Empty, item);
            }
            
        }
    }
}