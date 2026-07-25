using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Represents a handler for commands that produce a strongly-typed result.
/// </summary>
/// <typeparam name="TCommand">The type of command this handler processes. Must implement <see cref="ICommand{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The type of result produced by the command.</typeparam>
/// <remarks>
/// Use this interface when you want type-safe handling of commands with a specific result type.
/// The handler processes the command asynchronously and returns the strongly-typed result.
/// </remarks>
public interface ICommandHandler<in TCommand, TResult>: ICommand, IAsyncHandler<TCommand, TResult> 
    where TCommand : ICommand<TResult>;