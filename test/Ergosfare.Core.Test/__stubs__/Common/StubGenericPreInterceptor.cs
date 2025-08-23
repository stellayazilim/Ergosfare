using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

internal class StubGenericPreInterceptor<TArg> : IAsyncPreInterceptor<StubNonGenericMessage>
{
    public Task HandleAsync(StubNonGenericMessage message, IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}