using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

public class StubGenericExceptionInterceptor<TArg>: IAsyncExceptionInterceptor<StubNonGenericMessage>
{
    
    public Task HandleAsync(StubNonGenericMessage message, object? messageResult, Exception exception, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}