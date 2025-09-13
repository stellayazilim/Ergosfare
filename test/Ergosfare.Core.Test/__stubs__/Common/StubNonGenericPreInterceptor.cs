using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

internal class StubNonGenericPreInterceptor: IAsyncPreInterceptor<StubNonGenericMessage>
{
    public Task<object> HandleAsync(StubNonGenericMessage message, IExecutionContext context)
    {
        return Task.FromResult<object>(message);
    }
}



internal class StubNonGenericDerivedPreInterceptor: IAsyncPreInterceptor<StubNonGenericDerivedMessage>
{
    public Task<object> HandleAsync(StubNonGenericDerivedMessage message, IExecutionContext context)
    {
        return Task.FromResult<object>(message);
    }
}


internal class StubNonGenericStreamPreInterceptorAbortExecution: IAsyncPreInterceptor<StubNonGenericMessage>
{
    public Task<object> HandleAsync(StubNonGenericMessage message, IExecutionContext context)
    {
        context.Abort();
        return Task.FromResult<object>(message);
    }
}
internal class StubNonGenericPreInterceptor2 : StubNonGenericPreInterceptor;


internal class StubNonGenericDerivedPreInterceptor2: StubNonGenericDerivedPreInterceptor;