using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Test.Fixtures.Stubs.Basic;


/// <summary>
/// A stub async handler for <see cref="StubMessage"/> that performs no operations.
/// Useful for testing handler registration and invocation without side effects.
/// </summary>
public class StubVoidAsyncHandler: IAsyncHandler<StubMessage>
{
    /// <summary>
    /// Handles a <see cref="StubMessage"/> asynchronously.
    /// This implementation completes immediately without performing any action.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>A completed <see cref="Task"/>.</returns>
    public Task HandleAsync(StubMessage message, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}

