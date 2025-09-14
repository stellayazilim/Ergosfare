using Ergosfare.Command.Test.__stubs__;
using Ergosfare.Commands.Abstractions;
using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Internal.Contexts;

namespace Ergosfare.Command.Test;

public class CommandPreInterceptorTResultTResultTests
{
    private class TestCommandTResultTResultPreInterceptor: ICommandPreInterceptor<StubNonGenericCommand, StubNonGenericCommand>
    {
        public Task<StubNonGenericCommand> HandleAsync(StubNonGenericCommand command, IExecutionContext context)
        {
            return Task.FromResult(command);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TestCommandPreInterceptorShouldImplement()
    {
        // arrange
        IAsyncPreInterceptor<StubNonGenericCommand> interceptor = new TestCommandTResultTResultPreInterceptor();
        
        // act
        var result = await interceptor.HandleAsync(new StubNonGenericCommand(), new ErgosfareExecutionContext(null, default));
        
        // asssert
        Assert.IsType<StubNonGenericCommand>(result);
    } 
}