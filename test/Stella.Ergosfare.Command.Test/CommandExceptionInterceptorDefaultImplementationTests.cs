using Stella.Ergosfare.Command.Test.__stubs__;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Handlers;

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
    // These probes are invoked directly by the tests below, never dispatched — deliberately
    // outside the compiled closure, which is what silences ERGO001 for the private type.
    [ExcludeFromDiscovery]
    private class TestCommandExceptionInterceptor : ICommandExceptionInterceptor<TestCommandStringResult, string>
    {
        public bool Called;

        public ValueTask<string?> HandleAsync(TestCommandStringResult command, string? result, Exception exception, ErgosfareContext context)
        {
            Called = true;
            return ValueTask.FromResult(result);
        }
    }

    [ExcludeFromDiscovery]
    private class TestCommandPostInterceptor : ICommandPostInterceptor<TestCommandStringResult, string>
    {
        public bool Called;

        public ValueTask<string> HandleAsync(TestCommandStringResult command, string commandResult, ErgosfareContext context)
        {
            Called = true;
            return ValueTask.FromResult(commandResult);
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
            new TestCommandStringResult(), "original", new Exception("boom"), new ErgosfareContext());

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
            new TestCommandStringResult(), "result", new ErgosfareContext());

        Assert.True(interceptor.Called);
        Assert.Equal("result", result);
    }

}
