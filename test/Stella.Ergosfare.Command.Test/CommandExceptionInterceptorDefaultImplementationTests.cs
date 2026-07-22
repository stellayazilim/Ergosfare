using Stella.Ergosfare.Command.Test.__stubs__;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Internal.Contexts;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// Verifies that the default interface implementations of the typed command interceptor
/// interfaces forward the untyped pipeline calls to their type-safe members.
/// </summary>
/// <remarks>
/// The stub interceptors are pass-through and record invocation on instance flags: module
/// tests register this whole assembly, so stubs must not alter pipeline results.
/// </remarks>
public class CommandExceptionInterceptorDefaultImplementationTests
{
    private class TestCommandExceptionInterceptor : ICommandExceptionInterceptor<TestCommandStringResult, string>
    {
        public bool Called;

        public Task<string?> HandleAsync(TestCommandStringResult command, string? result, Exception exception, IExecutionContext context)
        {
            Called = true;
            return Task.FromResult(result);
        }
    }

    private class TestCommandPostInterceptor : ICommandPostInterceptor<TestCommandStringResult, string>
    {
        public bool Called;

        public Task<string> HandleAsync(TestCommandStringResult command, string commandResult, IExecutionContext context)
        {
            Called = true;
            return Task.FromResult(commandResult);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ExceptionInterceptorDefaultImplementation_ShouldForwardToTypedHandleAsync()
    {
        // arrange
        var interceptor = new TestCommandExceptionInterceptor();

        // act — invoke through the root interface member the pipeline uses
        var result = await ((IAsyncExceptionInterceptor<TestCommandStringResult, string>) interceptor).HandleAsync(
            new TestCommandStringResult(), "original", new Exception("boom"), new ErgosfareExecutionContext(null, default));

        // assert
        Assert.True(interceptor.Called);
        Assert.Equal("original", result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task PostInterceptorDefaultImplementation_ShouldForwardToTypedHandleAsync()
    {
        var interceptor = new TestCommandPostInterceptor();

        var result = await ((IAsyncPostInterceptor<TestCommandStringResult, string>) interceptor).HandleAsync(
            new TestCommandStringResult(), "result", new ErgosfareExecutionContext(null, default));

        Assert.True(interceptor.Called);
        Assert.Equal("result", result);
    }

}
