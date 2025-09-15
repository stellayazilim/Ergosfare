using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__.Handlers;


public record class StubStringMessage(string Message): IMessage;
public record class IndirectStubStringMessage(string Message) : StubStringMessage(Message);

public class StubStringMessageHandler: IAsyncHandler<StubStringMessage, string>
{
    public Task<string> HandleAsync(StubStringMessage message, IExecutionContext context)
    {
        return Task.FromResult(message.Message);
    }
}


public class StubStringMessagePreInterceptorThrows: IAsyncPreInterceptor<StubStringMessage>
{
    public Task<object> HandleAsync(StubStringMessage message, IExecutionContext context)
    {
        throw new Exception(nameof(StubStringMessagePreInterceptorThrows));
    }
}


public class StubStringMessagePostInterceptor: IAsyncPostInterceptor<StubStringMessage, string>
{
    public Task<object> HandleAsync(StubStringMessage message, string? messageResult, IExecutionContext context)
    {
        return Task.FromResult<object>(messageResult ?? "empty result");
    }
}

public class IndirectStubStringMessagePostInterceptor:  IAsyncPostInterceptor<IndirectStubStringMessage, string>
{
    public Task<object> HandleAsync(IndirectStubStringMessage message, string? messageResult, IExecutionContext context)
    {
        return Task.FromResult<object>(messageResult ?? "empty result");
    }
}


public class StubStringMessageExceptionInterceptor: IAsyncExceptionInterceptor<StubStringMessage, string>
{
    public Task<object> HandleAsync(StubStringMessage message, string? result, Exception exception, IExecutionContext context)
    {
        return Task.FromResult<object>(result ?? "empty result");
    }
}

public class StubStringMessageExceptionInterceptorModifyResult: IAsyncExceptionInterceptor<StubStringMessage, string>
{
    public Task<object> HandleAsync(StubStringMessage message, string? result, Exception exception, IExecutionContext context)
    {
        return Task.FromResult<object>("modified result");
    }
}


public class StubStringExceptionInterceptorReturnsNull: IAsyncExceptionInterceptor<StubStringMessage, string>
{
    public Task<object> HandleAsync(StubStringMessage message, string? result, Exception exception, IExecutionContext context)
    {
        return Task.FromResult<object>(null);
    }
}

public class IndirectStubStringExceptionInterceptorReturnsNull : StubStringExceptionInterceptorReturnsNull;

public class IndirectStubStringExceptionInterceptor: IAsyncExceptionInterceptor<IndirectStubStringMessage, string>
{
    public static bool IsRuned;
    public Task<object> HandleAsync(IndirectStubStringMessage message, string? result, Exception exception, IExecutionContext context)
    {
        IsRuned = true;
        return Task.FromResult<object>(null);
    }
}

public class StubStringFinalInterceptor: IAsyncFinalInterceptor<StubStringMessage, string>
{
    public static bool IsRuned;
    public Task HandleAsync(StubStringMessage message, string? result, Exception? exception, IExecutionContext context)
    {
        IsRuned = true;
        return Task.CompletedTask;
    }
}