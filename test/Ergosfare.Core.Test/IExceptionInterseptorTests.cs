using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Test.__stubs__;
using ExecutionContext = Ergosfare.Core.Internal.Contexts.ExecutionContext;

namespace Ergosfare.Core.Test;

public class ExceptionInterceptorTests
{

    private class TestExceptionInterceptor1: IExceptionInterceptor
    {

        public object Handle(object message, object? result, Exception exception, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
    
    
    private class TestExceptionInterceptor2: IExceptionInterceptor<StubMessages.StubNonGenericMessage, Task>
    {
        public object Handle(StubMessages.StubNonGenericMessage message, Task? result, Exception exception, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }


    private class TestExceptionInterceptor3: IAsyncExceptionInterceptor<StubMessages.StubNonGenericMessage>
    {
        public Task HandleAsync(StubMessages.StubNonGenericMessage message, object? result, Exception exception, IExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    
    private class TestExceptionInterceptor4: IAsyncExceptionInterceptor<StubMessages.StubNonGenericMessage, Task>
    {
        public Task HandleAsync(StubMessages.StubNonGenericMessage message, Task? result, Exception exception, IExecutionContext context,
            CancellationToken cancellation = default)
        {
            return Task.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Coverage")]
    public async Task IExceptionInterceptorImplements()
    {
        var testInterceptor = new TestExceptionInterceptor1(); 
        var result = Task.CompletedTask; 
        var message = new StubMessages.StubNonGenericMessage(); 
        var exception = new Exception(); 
        var ct = CancellationToken.None; 
        var values = new Dictionary<object, object?>(); 
        var context = new ExecutionContext(ct, values); 
        IExceptionInterceptor interceptor1 = new TestExceptionInterceptor1(); 
        IExceptionInterceptor interceptor2 = new TestExceptionInterceptor2(); 
        IExceptionInterceptor<StubMessages.StubNonGenericMessage, object> interceptor3 = new TestExceptionInterceptor3(); 
        IExceptionInterceptor<StubMessages.StubNonGenericMessage, Task> interceptor4 = new TestExceptionInterceptor4();
        await using (AmbientExecutionContext.CreateScope(new ExecutionContext(ct, values)))
        {
            var result1 = interceptor1.Handle(message, result, exception, AmbientExecutionContext.Current); 
            Assert.NotNull(result1);
        }

        await using (AmbientExecutionContext.CreateScope(new ExecutionContext(ct, values)))
        {
            var result2 = interceptor2.Handle(message, result, exception, AmbientExecutionContext.Current); 
            Assert.NotNull(result2); 
            await Assert.IsType<Task>(result2, exactMatch: false);
        }

        await using (AmbientExecutionContext.CreateScope(new ExecutionContext(ct, values)))
        {
            var result3 = interceptor3.Handle(message, result, exception, AmbientExecutionContext.Current); 
            Assert.NotNull(result3); 
            await Assert.IsType<Task>(result3, exactMatch: false);
        }

        await using (AmbientExecutionContext.CreateScope(new ExecutionContext(ct, values)))
        {
            var result4 = interceptor4.Handle(message, result, exception, AmbientExecutionContext.Current); 
            Assert.NotNull(result4);
            await Assert.IsType<Task>(result4, exactMatch: false);
        }
    }
}