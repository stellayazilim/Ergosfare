using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
// ReSharper disable ClassNeverInstantiated.Global

namespace Stella.Ergosfare.Command.Test.__stubs__;


/// <summary>
/// A test command handler for <see cref="TestCommand"/>.
/// Implements <see cref="ICommandHandler{TCommand}"/> for testing purposes.
/// </summary>
public class TestCommandHandler: ICommandHandler<TestCommand>
{
    
    /// <summary>
    /// Handles the <see cref="TestCommand"/> asynchronously.
    /// </summary>
    /// <param name="message">The command to handle.</param>
    /// <param name="context">The execution context of the command.</param>
    /// <returns>A completed <see cref="Task"/>.</returns>
    public ValueTask HandleAsync(TestCommand message, IExecutionContext context)
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Handles <see cref="TestCommandStringResult"/> commands and returns a <see cref="string"/> result.
/// This is typically used as a test or stub implementation.
/// </summary>
public class TestCommandStringResultHandler:ICommandHandler<TestCommandStringResult, string>
{
    
    /// <summary>
    /// Handles the given <see cref="TestCommandStringResult"/> command asynchronously.
    /// </summary>
    /// <param name="message">The command to handle.</param>
    /// <param name="context">The execution context providing services and metadata for the command.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> containing the result of the command. 
    /// In this stub implementation, it always returns an empty string.
    /// </returns>
    public ValueTask<string> HandleAsync(TestCommandStringResult message, IExecutionContext context)
    {
        return ValueTask.FromResult(string.Empty);
    }
}