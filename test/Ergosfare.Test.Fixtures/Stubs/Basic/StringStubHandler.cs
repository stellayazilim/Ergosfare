using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Test.Fixtures.Stubs.Basic;

/// <summary>
/// A stub synchronous handler for <see cref="StubMessage"/> that returns a fixed string result.
/// Useful for testing result propagation and adapters.
/// </summary>
public class StubStringHandler : IHandler<StubMessage, string>
{
    /// <summary>
    /// The predefined result returned by this handler.
    /// </summary>
    public const string Result = "Hello world";

    /// <summary>
    /// Handles a <see cref="StubMessage"/> synchronously and returns a string result.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>The fixed <see cref="Result"/> string.</returns>
    public string Handle(StubMessage message, IExecutionContext context)
    {
        return Result;
    }
}