using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Contracts.Attributes;
using Stella.Ergosfare.Core.Abstractions;
// ReSharper disable ClassNeverInstantiated.Global
#pragma warning disable CS0618 // Type or member is obsolete

namespace Stella.Ergosfare.Command.Test.__stubs__;



/// <summary>
/// A stub handler for <see cref="StubNonGenericCommand"/> used in tests.
/// Tracks whether the handler was called.
/// </summary>
[Group( "default","group1", "group2")]
public class StubNonGenericCommandHandler: ICommandHandler<StubNonGenericCommand>
{
    /// <summary>
    /// Indicates whether <see cref="HandleAsync"/> has been invoked.
    /// </summary>
    public static bool HasCalled;
    
    /// <summary>
    /// Handles the given <see cref="StubNonGenericCommand"/> asynchronously.
    /// Sets <see cref="HasCalled"/> to true when invoked.
    /// </summary>
    /// <param name="message">The command message to handle.</param>
    /// <param name="context">The execution context for the handler.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task HandleAsync(StubNonGenericCommand message, IExecutionContext context)
    {
        HasCalled = true;
        return Task.CompletedTask;
    }
}



/// <summary>
/// A stub handler for <see cref="StubNonGenericCommandStringResult"/> used in tests.
/// Tracks whether the handler was called and returns a <see cref="string"/> result.
/// </summary>
public class StubNonGenericCommandStringResultHandler: ICommandHandler<StubNonGenericCommandStringResult, string>
{
    /// <summary>
    /// Indicates whether <see cref="HandleAsync"/> has been invoked.
    /// </summary>
    public static bool HasCalled;
    
    /// <summary>
    /// Handles the given <see cref="StubNonGenericCommandStringResult"/> asynchronously.
    /// Sets <see cref="HasCalled"/> to true and returns an empty string.
    /// </summary>
    /// <param name="message">The command message to handle.</param>
    /// <param name="context">The execution context for the handler.</param>
    /// <returns>A <see cref="Task{TResult}"/> with a string result.</returns>
    public Task<string> HandleAsync(StubNonGenericCommandStringResult message, IExecutionContext context)
    {
        HasCalled = true;
        return Task.FromResult(string.Empty);
    }
}