using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

public class StubNonGenericFinalInterceptor: IAsyncFinalInterceptor<StubNonGenericMessage, Task>
{
    public Task HandleAsync(StubNonGenericMessage message, Task? result, Exception? exception, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}