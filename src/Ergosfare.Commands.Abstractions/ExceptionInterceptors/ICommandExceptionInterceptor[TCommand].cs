using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;


/// <summary>
/// Marker interface for asynchronous exception interceptors for commands.
/// Inherits <see cref="IAsyncExceptionInterceptor{TMessage,TResult}"/> and <see cref="ICommand"/>
/// to allow registration within the command module.
/// This interface does not modify the behavior or return type; interception logic
/// is handled by <see cref="IAsyncExceptionInterceptor{TMessage,TResult}"/>.
/// </summary>
/// <typeparam name="TCommand">
/// The type of command being intercepted. Must implement <see cref="ICommand"/>
/// </typeparam>
/// <remarks>
/// <c>ICommandExceptionInterceptor&lt;in TCommand, in Task, Task&gt;</c>
/// or other type-safe variants, which preserve the exact result type.
/// </remarks>
public interface ICommandExceptionInterceptor<in TCommand>: ICommand,  IAsyncExceptionInterceptor<TCommand, object> where TCommand : ICommand;