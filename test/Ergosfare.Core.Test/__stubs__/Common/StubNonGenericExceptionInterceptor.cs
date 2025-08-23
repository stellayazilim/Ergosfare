using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;


internal class StubNonGenericExceptionInterceptor: IAsyncExceptionInterceptor<StubNonGenericMessage>
{
    public Task HandleAsync(StubNonGenericMessage message, object? messageResult, Exception exception, IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

internal class StubNonGenericDerivedExceptionInterceptor: IAsyncExceptionInterceptor<StubNonGenericDerivedMessage>
{
    public Task HandleAsync(StubNonGenericDerivedMessage message, object? messageResult, Exception exception,
        IExecutionContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}



internal class StubNonGenericStringResultExceptionInterceptor: IExceptionInterceptor<StubNonGenericMessage, string>
{
    public object Handle(StubNonGenericMessage message, string? messageResult, Exception exception, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}

internal class StubNonGenericAsyncExceptionInterceptor: IAsyncExceptionInterceptor<StubNonGenericMessage>
{

    public Task HandleAsync(StubNonGenericMessage message, object? messageResult, Exception exception, IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}


public class StubNonGenericAsyncStringResultExceptionInterceptor: IAsyncExceptionInterceptor<StubNonGenericMessage, string>
{
    public Task HandleAsync(StubNonGenericMessage message, string? result, Exception exception, IExecutionContext context,
        CancellationToken cancellation = default)
    {
        return Task.CompletedTask;
    }
}


internal class StubNonGenericExceptionInterceptor2: StubNonGenericExceptionInterceptor;

internal class StubNonGenericDerivedExceptionInterceptor2 : StubNonGenericDerivedExceptionInterceptor; 