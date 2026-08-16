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

        public ValueTask<string> HandleAsync(TestCommandStringResult command, string? result, Exception exception, ErgosfareContext context)
        {
            Called = true;

            // Handling means answering: the slot is empty because the handler threw before
            // it produced anything.
            return ValueTask.FromResult(result ?? string.Empty);
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

    /// <summary>
    /// A module-wide policy: every command, one exception flavour. Extending
    /// <see cref="ICommandExceptionInterceptorFor{TException}"/> makes the type an
    /// <see cref="ICommand"/> too, which is why it is kept out of discovery.
    /// </summary>
    [ExcludeFromDiscovery]
    private sealed class TimeoutPolicy : ICommandExceptionInterceptorFor<TimeoutException>
    {
        public TimeoutException? Seen;

        public ValueTask<object> HandleAsync(ICommand command, object? messageResult, TimeoutException exception,
            ErgosfareContext context)
        {
            Seen = exception;
            return ValueTask.FromResult(messageResult ?? "recovered");
        }
    }

    /// <summary>A flavour of the accepted exception, to prove the filter matches derived types.</summary>
    private sealed class RequestTimeoutException : TimeoutException;

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task FilteredModuleWideInterceptor_CastsTheExceptionToItsDeclaredFlavour()
    {
        var interceptor = new TimeoutPolicy();
        var thrown = new RequestTimeoutException();

        // The pipeline only ever calls the untyped member; the default body is what turns
        // the Exception it was handed into the flavour the policy was written against.
        var result = await ((IAsyncExceptionInterceptor<ICommand>) interceptor).HandleAsync(
            new TestCommand(), null, thrown, new ErgosfareContext());

        Assert.Same(thrown, interceptor.Seen);
        Assert.Equal("recovered", result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void FilteredModuleWideInterceptor_MatchesWithCatchSemantics()
    {
        var filter = (IExceptionInterceptorFilter) new TimeoutPolicy();

        // The cast in the default body cannot fail because this is what runs first: the
        // stage asks the filter before it invokes the interceptor.
        Assert.True(filter.Matches(new TimeoutException()));
        Assert.True(filter.Matches(new RequestTimeoutException()));
        Assert.False(filter.Matches(new InvalidOperationException()));
    }
}
