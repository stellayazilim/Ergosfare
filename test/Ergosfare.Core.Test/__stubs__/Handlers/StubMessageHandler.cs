using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__.Handlers;


// non-generic message
public record StubMessage: IMessage;

// derived non-generic message from StubMessage
public record IndirectStubMessage: StubMessage;

// non-generic diffirent message from StubMessage
public record StrangeStubMessage : IMessage;




// StubMessage handler
public class StubMessageHandler: IAsyncHandler<StubMessage>
{
    public Task HandleAsync(StubMessage message, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}

// StubMessage pre-interceptor
public class StubMessagePreInterceptor: IAsyncPreInterceptor<StubMessage>
{
    public Task<object> HandleAsync(StubMessage message, IExecutionContext context)
    {
        return Task.FromResult<object>(message);
    }
}


// StubMessage post-interceptor
public class StubMessagePostInterceptor: IAsyncPostInterceptor<StubMessage>
{
    public Task<object> HandleAsync(StubMessage message, object? _, IExecutionContext context)
    {
        return Task.FromResult<object>(Task.CompletedTask);
    }
}


// StubMessage exception-interceptor
public class StubMessageExceptionInterceptor: IAsyncExceptionInterceptor<StubMessage>
{
    public Task HandleAsync(StubMessage message, object? messageResult, Exception exception, IExecutionContext context)
    {
        return Task.FromResult(messageResult);
    }
}

public class DerivedStubMessageExceptionInterceptor : StubMessageExceptionInterceptor;
public class IndirectStubMessageExceptionInterceptor: IAsyncExceptionInterceptor<IndirectStubMessage>
{
    public Task HandleAsync(IndirectStubMessage message, object? messageResult, Exception exception, IExecutionContext context)
    {
        return Task.FromResult(messageResult);
    }
}