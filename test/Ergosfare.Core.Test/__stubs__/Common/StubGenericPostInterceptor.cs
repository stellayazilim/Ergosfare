using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

internal class StubGenericPostInterceptor<TArg>: IAsyncPostInterceptor<StubNonGenericMessage>
{
    public Task<object> HandleAsync(StubNonGenericMessage message, object? messageResult, IExecutionContext context)
    {
        return Task.FromResult(new object());
    }

    public object Handle(StubNonGenericMessage message, object? messageResult, IExecutionContext context)
    {
        throw new NotImplementedException();
    }
}