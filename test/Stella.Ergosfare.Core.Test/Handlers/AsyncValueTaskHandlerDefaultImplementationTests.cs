using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Internal.Contexts;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;
#pragma warning disable ERGOEXP001 // deliberately exercising the experimental ValueTask handler

namespace Stella.Ergosfare.Core.Test.Handlers;

/// <summary>
/// Verifies that the default interface implementation of
/// <see cref="IAsyncValueTaskHandler{TMessage, TResult}"/> bridges the synchronous
/// <see cref="IHandler{TMessage, TResult}.Handle"/> member to the async member.
/// </summary>
public class AsyncValueTaskHandlerDefaultImplementationTests
{
    private class TestValueTaskHandler : IAsyncValueTaskHandler<StubMessage, string>
    {
        public const string Result = "value-task-result";

        public ValueTask<string> HandleAsync(StubMessage message, IExecutionContext context)
        {
            return ValueTask.FromResult(Result);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task DefaultImplementation_ShouldBridgeHandleToHandleAsync()
    {
        IHandler<StubMessage, ValueTask<string>> handler = new TestValueTaskHandler();

        var result = await handler.Handle(new StubMessage(), new ErgosfareExecutionContext(null, default));

        Assert.Equal(TestValueTaskHandler.Result, result);
    }
}
