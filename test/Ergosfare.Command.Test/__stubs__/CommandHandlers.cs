using Ergosfare.Context;
using Ergosfare.Contracts;

namespace Ergosfare.Command.Test.__stubs__;

public class StubNonGenericCommandHandler: ICommandHandler<StubNonGenericCommand>
{
    public Task HandleAsync(StubNonGenericCommand message, IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}


public class StubNonGenericCommandStringResultHandler: ICommandHandler<StubNonGenericCommandStringResult, string>
{
    public Task<string> HandleAsync(StubNonGenericCommandStringResult message, IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }
}