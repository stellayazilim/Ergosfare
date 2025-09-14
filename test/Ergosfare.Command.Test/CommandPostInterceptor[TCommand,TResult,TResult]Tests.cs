using Ergosfare.Command.Test.__stubs__;
using Ergosfare.Commands.Abstractions;
using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Command.Test;

public class CommandPostInterceptorTCommandTResultTResultTests
{
    
    private class TestCommandTCommandTResultTResultPostInterceptor: ICommandPostInterceptor<StubNonGenericCommandStringResult, string, string>
    {
        public Task<string> HandleAsync(StubNonGenericCommandStringResult command, string commandResult, IExecutionContext context)
        {
            return Task.FromResult(commandResult);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TestCommandPostInterceptorShouldImplement()
    {
        // arrange
        IAsyncPostInterceptor<StubNonGenericCommandStringResult, string> interceptor = new TestCommandTCommandTResultTResultPostInterceptor();
        
        // act 
        var result = await interceptor.HandleAsync(new StubNonGenericCommandStringResult(), String.Empty, null);
        
        // assert
        Assert.IsType<string>(result);

    }
}