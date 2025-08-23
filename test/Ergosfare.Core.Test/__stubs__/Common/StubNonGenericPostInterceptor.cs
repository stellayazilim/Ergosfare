using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

internal class StubNonGenericPostInterceptor: IAsyncPostInterceptor<StubNonGenericMessage,Task>
{
    public async Task HandleAsync(StubNonGenericMessage message, Task? messageResult, IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
               
    }
}


internal class StubNonGenericDerivedPostInterceptor: IAsyncPostInterceptor<StubNonGenericDerivedMessage,Task>
{
    public async Task HandleAsync(StubNonGenericDerivedMessage message, Task? messageResult, IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
    }
}

internal class StubNonGenericPostInterceptor2 : StubNonGenericPostInterceptor;
internal class StubNonGenericDerivedPostInterceptor2: StubNonGenericDerivedPostInterceptor;