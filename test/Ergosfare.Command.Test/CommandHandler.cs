using Ergosfare.Context;
using Ergosfare.Contracts;

namespace Ergosfare.Command.Test;

public class TestCommandHandler: ICommandHandler<TestCommand>
{
    public Task HandleAsync(TestCommand message, IExecutionContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}


public class TestCommandStringResultHandler:ICommandHandler<TestCommandStringResult, string>
{
    public Task<string> HandleAsync(TestCommandStringResult message, IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }
}