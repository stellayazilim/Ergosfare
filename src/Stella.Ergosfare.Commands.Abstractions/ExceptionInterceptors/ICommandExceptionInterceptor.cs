using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Handles failures raised while dispatching any command, whatever its type.
/// </summary>
/// <remarks>
/// Because it accepts every command, this contract sees the command as
/// <see cref="ICommand"/> and its result as <see cref="object"/>. Running is what marks the
/// failure handled, so an interceptor this broad handles everything it is registered for —
/// use <see cref="ICommandExceptionInterceptorFor{TException}"/> to narrow it by exception
/// type, or <see cref="ICommandExceptionInterceptor{TCommand, TResult}"/> for a typed
/// result.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandExceptionInterceptor: ICommand, IAsyncExceptionInterceptor<ICommand>;
