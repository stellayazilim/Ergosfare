using Stella.Ergosfare.Commands.Abstractions;

namespace Stella.Ergosfare.Command.Test.__stubs__;


/// <summary>
/// A stub command used for testing non-generic command handlers.
/// Implements <see cref="ICommand"/>.
/// </summary>
public record StubNonGenericCommand: ICommand;



/// <summary>
/// A stub command used for testing non-generic command handlers that return a <see cref="string"/> result.
/// Implements <see cref="ICommand{TResult}"/> with <see cref="string"/> as the result type.
/// </summary>

[Obsolete("Use TestCommandStringResult instead.")]
public record StubNonGenericCommandStringResult: ICommand<string>;





/// <summary>
/// A stub command served by an ungrouped handler, for the ungrouped mediator overloads.
/// The grouped stub's handler carries group attributes, which keeps its default-set
/// pipeline out of the compiled plans — this one's pipeline is the plain single-handler
/// shape the generator always plans.
/// </summary>
public record StubPlainCommand : ICommand;

/// <summary>
/// Represents a test command with no return value.
/// </summary>
public record TestCommand: ICommand;

/// <summary>
/// Represents a test command that produces a string result.
/// </summary>
public record TestCommandStringResult:ICommand<string>;