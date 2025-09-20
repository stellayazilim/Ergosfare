using Ergosfare.Commands.Abstractions;

namespace Ergosfare.Command.Test.__stubs__;


/// <summary>
/// A stub command used for testing non-generic command handlers.
/// Implements <see cref="ICommand"/>.
/// </summary>
[Obsolete("Use TestCommand instead.")]
public record StubNonGenericCommand: ICommand;



/// <summary>
/// A stub command used for testing non-generic command handlers that return a <see cref="string"/> result.
/// Implements <see cref="ICommand{TResult}"/> with <see cref="string"/> as the result type.
/// </summary>

[Obsolete("Use TestCommandStringResult instead.")]
public record StubNonGenericCommandStringResult: ICommand<string>;





/// <summary>
/// Represents a test command with no return value.
/// </summary>
public record TestCommand: ICommand;

/// <summary>
/// Represents a test command that produces a string result.
/// </summary>
public record TestCommandStringResult:ICommand<string>;