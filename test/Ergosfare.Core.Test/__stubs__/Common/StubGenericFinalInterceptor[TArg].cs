using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

public class StubGenericFinalInterceptor<TArg>: IAsyncFinalInterceptor<StubNonGenericMessage>
{
    public Task HandleAsync(StubNonGenericMessage message, object? result, Exception? exception, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}