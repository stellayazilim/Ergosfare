using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Handles commands of type <typeparamref name="TCommand"/> that return nothing.
/// </summary>
/// <typeparam name="TCommand">The command type this handler accepts.</typeparam>
/// <remarks>
/// A command is sent to exactly one handler, so registering two for the same command type
/// fails the dispatch. Implement <see cref="ICommandHandler{TCommand, TResult}"/> for a
/// command that returns a value.
/// </remarks>
public interface ICommandHandler<in TCommand> : ICommand, IAsyncHandler<TCommand>
    where TCommand : ICommand;
