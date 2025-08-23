using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

public class StubGenericExceptionInterceptor<TArg>: IAsyncExceptionInterceptor<StubNonGenericMessage, Task>
{
    public Task HandleAsync(StubNonGenericMessage message, Task? result, Exception exception, IExecutionContext context,
        CancellationToken cancellation = default)
    {
        return Task.CompletedTask;
    }
}