using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

public class StubGenericHandler<TArg>: IHandler<StubGenericMessage<TArg>, Task>
{
    public Task Handle(StubGenericMessage<TArg> message, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}
