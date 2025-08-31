using Ergosfare.Context;
using Ergosfare.Contracts;
using Ergosfare.Contracts.Attributes;

namespace Ergosfare.Command.Test.__stubs__;

[Group( "default","group1", "group2")]
public class StubNonGenericCommandHandler: ICommandHandler<StubNonGenericCommand>
{
    public static bool HasCalled;
    public Task HandleAsync(StubNonGenericCommand message, IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        HasCalled = true;
        return Task.CompletedTask;
    }
}




public class StubNonGenericCommandStringResultHandler: ICommandHandler<StubNonGenericCommandStringResult, string>
{
    
    public static bool HasCalled;
    public Task<string> HandleAsync(StubNonGenericCommandStringResult message, IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        HasCalled = true;
        return Task.FromResult(string.Empty);
    }
}