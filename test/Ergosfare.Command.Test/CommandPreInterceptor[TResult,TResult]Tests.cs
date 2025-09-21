using Ergosfare.Command.Test.__stubs__;
using Ergosfare.Commands.Abstractions;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Internal.Contexts;
#pragma warning disable CS0618 // Type or member is obsolete

namespace Ergosfare.Command.Test;


/// <summary>
/// Contains unit tests for command pre-interceptors implementing
/// <see cref="ICommandPreInterceptor{TCommand, TResult}"/>.
/// </summary>
public class CommandPreInterceptorTResultTResultTests
{
    
    /// <summary>
    /// A test implementation of <see cref="ICommandPreInterceptor{TCommand, TResult}"/>
    /// for <see cref="StubNonGenericCommand"/> commands that returns the same command instance it receives.
    /// </summary>
    private class TestCommandTResultTResultPreInterceptor: ICommandPreInterceptor<StubNonGenericCommand, StubNonGenericCommand>
    {
        
        /// <summary>
        /// Handles the command pre-interception asynchronously.
        /// </summary>
        /// <param name="command">The command to intercept.</param>
        /// <param name="context">The execution context.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> containing the same <paramref name="command"/> instance.
        /// </returns>
        public Task<StubNonGenericCommand> HandleAsync(StubNonGenericCommand command, IExecutionContext context)
        {
            return Task.FromResult(command);
        }
    }

    
    /// <summary>
    /// Tests that <see cref="TestCommandTResultTResultPreInterceptor"/> correctly implements
    /// <see cref="IAsyncPreInterceptor{TCommand}"/> and returns a result of type <see cref="StubNonGenericCommand"/>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TestCommandPreInterceptorShouldImplement()
    {
        // arrange
        IAsyncPreInterceptor<StubNonGenericCommand> interceptor = new TestCommandTResultTResultPreInterceptor();
        
        // act
        var result = await interceptor.HandleAsync(new StubNonGenericCommand(), new ErgosfareExecutionContext(null,null,null, default));
        
        // asssert
        Assert.IsType<StubNonGenericCommand>(result);
    } 
}