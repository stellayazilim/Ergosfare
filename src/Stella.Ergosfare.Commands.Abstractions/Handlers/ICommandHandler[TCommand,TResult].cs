using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Handles commands of type <typeparamref name="TCommand"/> and returns the
/// <typeparamref name="TResult"/> they declare.
/// </summary>
/// <typeparam name="TCommand">The command type this handler accepts.</typeparam>
/// <typeparam name="TResult">The result type the command declares.</typeparam>
/// <remarks>
/// The result type comes from the command itself, so the caller and the handler cannot
/// disagree about it. A command is sent to exactly one handler.
/// </remarks>
public interface ICommandHandler<in TCommand, TResult>: ICommand, IAsyncHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>;
