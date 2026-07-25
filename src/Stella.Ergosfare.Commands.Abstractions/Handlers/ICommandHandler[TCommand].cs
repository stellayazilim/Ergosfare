using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Represents a handler for commands implementing <see cref="ICommand"/>.
/// </summary>
/// <typeparam name="TCommand">The type of command this handler processes. Must implement <see cref="ICommand"/>.</typeparam>
/// <remarks>
/// This interface is non-generic regarding the result type; the command may or may not produce a result.
/// For commands with a strongly typed result, consider using <see cref="ICommandHandler{TCommand, TResult}"/>.
/// </remarks>
public interface ICommandHandler<in TCommand> : ICommand, IAsyncHandler<TCommand>
    where TCommand : ICommand;