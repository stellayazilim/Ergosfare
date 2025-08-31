using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Test.__stubs__;

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
    
    
    private class TestExceptionInterceptor2: IExceptionInterceptor<StubNonGenericMessage, Task>
    {
        public object Handle(StubNonGenericMessage message, Task? result, Exception exception, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }


    private class TestExceptionInterceptor3: IAsyncExceptionInterceptor<StubNonGenericMessage>
    {
        public Task HandleAsync(StubNonGenericMessage message, object? result, Exception exception, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }

    
    private class TestExceptionInterceptor4: IAsyncExceptionInterceptor<StubNonGenericMessage, Task>
    {
        public Task HandleAsync(StubNonGenericMessage message, Task? result, Exception exception, IExecutionContext context)
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
        var message = new StubNonGenericMessage(); 
        var exception = new Exception(); 
        var ct = CancellationToken.None; 
        var values = new Dictionary<object, object?>();
        var context = StubExecutionContext.Create();
        IExceptionInterceptor interceptor1 = new TestExceptionInterceptor1(); 
        IExceptionInterceptor interceptor2 = new TestExceptionInterceptor2(); 
        IExceptionInterceptor<StubNonGenericMessage, object> interceptor3 = new TestExceptionInterceptor3(); 
        IExceptionInterceptor<StubNonGenericMessage, Task> interceptor4 = new TestExceptionInterceptor4();
        await using (AmbientExecutionContext.CreateScope(context))
        {
            var result1 = interceptor1.Handle(message, result, exception, AmbientExecutionContext.Current); 
            Assert.NotNull(result1);
        }

        await using (AmbientExecutionContext.CreateScope(context))
        {
            var result2 = interceptor2.Handle(message, result, exception, AmbientExecutionContext.Current); 
            Assert.NotNull(result2); 
            await Assert.IsType<Task>(result2, exactMatch: false);
        }

        await using (AmbientExecutionContext.CreateScope(context))
        {
            var result3 = interceptor3.Handle(message, result, exception, AmbientExecutionContext.Current); 
            Assert.NotNull(result3); 
            await Assert.IsType<Task>(result3, exactMatch: false);
        }

        await using (AmbientExecutionContext.CreateScope(context))
        {
            var result4 = interceptor4.Handle(message, result, exception, AmbientExecutionContext.Current); 
            Assert.NotNull(result4);
            await Assert.IsType<Task>(result4, exactMatch: false);
        }
    }
}