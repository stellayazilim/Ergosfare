using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

public class StubExceptionInterceptorModifyResult: IAsyncExceptionInterceptor<StubNonGenericMessage, string>
{
    public Task<object> HandleAsync(StubNonGenericMessage message, string? result, Exception exception, IExecutionContext context)
    {
        return Task.FromResult<object>("updated message");
    }
}