using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

internal class StubGenericPostInterceptor<TArg>: IAsyncPostInterceptor<StubNonGenericMessage, Task>
{
    public Task HandleAsync(StubNonGenericMessage message, Task? messageResult, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}