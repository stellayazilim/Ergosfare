using Stella.Ergosfare.Command.Test.__stubs__;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Internal.Contexts;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// Verifies that the default interface implementation of
/// <see cref="ICommandExceptionInterceptor{TCommand, TResult, TModifiedResult}"/> forwards
/// the untyped pipeline call to the type-safe member.
/// </summary>
public class CommandExceptionInterceptorDefaultImplementationTests
{
    private class TestCommandExceptionInterceptor : ICommandExceptionInterceptor<TestCommandStringResult, string, string>
    {
        public Task<string?> HandleAsync(TestCommandStringResult command, string? result, Exception? exception, IExecutionContext context)
        {
            return Task.FromResult<string?>("modified");
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task DefaultImplementation_ShouldForwardToTypedHandleAsync()
    {
        // arrange
        IAsyncExceptionInterceptor<TestCommandStringResult, string> interceptor = new TestCommandExceptionInterceptor();

        // act — invoke through the root interface member the pipeline uses
        var result = await interceptor.HandleAsync(
            new TestCommandStringResult(), "original", new Exception("boom"), new ErgosfareExecutionContext(null, default));

        // assert
        Assert.Equal("modified", result);
    }
}
