using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

public class StubNonGenericStreamExceptionInterceptorThrowsException: IExceptionInterceptor<StubNonGenericMessage, IAsyncEnumerable<string>>
{
    public object Handle(StubNonGenericMessage message, IAsyncEnumerable<string>? messageResult, Exception exception,
        IExecutionContext context)
    {
        throw exception;
    }
}