using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;


/// <summary>
/// Marker interface for asynchronous exception interceptors for commands.
/// Inherits <see cref="IAsyncExceptionInterceptor{TCommand, TResult}"/> and <see cref="ICommand"/>
/// to allow registration within the Command module.
/// This interface does not modify the behavior or return type; the interception logic
/// is handled entirely by <see cref="IAsyncExceptionInterceptor{TCommand, TResult}"/>.
/// </summary>
/// <typeparam name="TCommand">
/// The command type being intercepted. Must implement <see cref="ICommand{TResult}"/>.
/// </typeparam>
/// <typeparam name="TResult">
/// The result type of the command.
/// </typeparam>
public interface ICommandExceptionInterceptor<in TCommand, in TResult>:ICommand, IAsyncExceptionInterceptor<TCommand, TResult>
    where TCommand : ICommand<TResult>;