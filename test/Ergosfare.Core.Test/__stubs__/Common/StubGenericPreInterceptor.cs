using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

internal class StubGenericPreInterceptor<TArg> : IAsyncPreInterceptor<StubNonGenericMessage>
{
    public Task<object> HandleAsync(StubNonGenericMessage message, IExecutionContext context)
    {
        return Task.FromResult<object>(message);
    }
}