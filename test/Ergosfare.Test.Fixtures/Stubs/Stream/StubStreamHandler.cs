using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Test.Fixtures.Stubs.Stream;


/// <summary>
/// A simple stub message used for testing stream-based handlers.
/// </summary>
public record StubStreamMessage : IMessage;

/// <summary>
/// An indirect stub message derived from <see cref="StubStreamMessage"/>.
/// Useful for testing type inheritance scenarios in message handling.
/// </summary>
public record IndirectStubStreamMessage : StubStreamMessage;


/// <summary>
/// An unrelated stub message that does not inherit from <see cref="StubStreamMessage"/>.
/// Can be used to verify that handlers do not process unrelated messages.
/// </summary>
public record UnrelatedStubStreamMessage : IMessage;



/// <summary>
/// A stub implementation of <see cref="IStreamHandler{TMessage, TResult}"/> for <see cref="StubStreamMessage"/>.
/// Produces a predefined asynchronous stream of string results for testing purposes.
/// </summary>
public class StubStreamHandler : IStreamHandler<StubStreamMessage, string>
{
    /// <summary>
    /// The sequence of string results to be yielded by <see cref="StreamAsync"/>.
    /// Can be updated dynamically to adjust test expectations.
    /// </summary>
    public static readonly string[] Results = [ "foo", "bar", "baz"];

    /// <summary>
    /// Streams the predefined <see cref="Results"/> asynchronously for the given <see cref="StubStreamMessage"/>.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <param name="context">The execution context provided by the test fixture.</param>
    /// <returns>An asynchronous stream of string results.</returns>
    public async IAsyncEnumerable<string> StreamAsync(StubStreamMessage message, IExecutionContext context)
    {
        // simulate async operation
        await Task.CompletedTask;

        // yield each predefined result
        foreach (var result in Results)
        {
            yield return result;
        }
    }
}


/// <summary>
/// A duplicate stub stream handler used to simulate multiple handlers for the same message.
/// Inherits behavior from <see cref="StubStreamHandler"/>.
/// </summary>
public class DuplicateStubStreamHandler : StubStreamHandler
{
    // Inherits everything from StubStreamHandler
}