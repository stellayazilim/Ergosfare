using Ergosfare.Command.Test.__stubs__;
using Ergosfare.Commands.Abstractions;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;
#pragma warning disable CS0618 // Type or member is obsolete

namespace Ergosfare.Command.Test;


/// <summary>
/// Contains unit tests for command post-interceptors implementing
/// <see cref="ICommandPostInterceptor{TCommand, TCommandResult, TResult}"/>.
/// </summary>
public class CommandPostInterceptorTCommandTResultTResultTests
{
    /// <summary>
    /// A test implementation of <see cref="ICommandPostInterceptor{TCommand, TCommandResult, TResult}"/>
    /// for <see cref="StubNonGenericCommandStringResult"/> commands that returns the same result it receives.
    /// </summary>
    private class TestCommandTCommandTResultTResultPostInterceptor: ICommandPostInterceptor<StubNonGenericCommandStringResult, string, string>
    {
        /// <summary>
        /// Handles the command post-interception asynchronously.
        /// </summary>
        /// <param name="command">The command that was executed.</param>
        /// <param name="commandResult">The result produced by the command.</param>
        /// <param name="context">The execution context.</param>
        /// <returns>A <see cref="Task{TResult}"/> that contains the same result as <paramref name="commandResult"/>.</returns>
        public Task<string?> HandleAsync(StubNonGenericCommandStringResult command, string? commandResult, IExecutionContext context)
        {
            return Task.FromResult(commandResult);
        }
    }

    
    /// <summary>
    /// Tests that <see cref="TestCommandTCommandTResultTResultPostInterceptor"/> correctly implements
    /// <see cref="IAsyncPostInterceptor{TCommand, TCommandResult}"/> and returns a result of type <see cref="string"/>.
    /// </summary>
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