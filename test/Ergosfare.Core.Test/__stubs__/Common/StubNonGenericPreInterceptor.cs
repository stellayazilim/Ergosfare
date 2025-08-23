using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

internal class StubNonGenericPreInterceptor: IAsyncPreInterceptor<StubNonGenericMessage>
{
    public Task HandleAsync(StubNonGenericMessage message, IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

internal class StubNonGenericDerivedPreInterceptor: IAsyncPreInterceptor<StubNonGenericDerivedMessage>
{
    public Task HandleAsync(StubNonGenericDerivedMessage message, IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

internal class StubNonGenericPreInterceptor2 : StubNonGenericPreInterceptor;


internal class StubNonGenericDerivedPreInterceptor2: StubNonGenericDerivedPreInterceptor;