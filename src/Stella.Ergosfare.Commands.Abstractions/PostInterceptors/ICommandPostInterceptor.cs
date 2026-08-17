using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;

/// <summary>
/// Runs after the handler of any command, whatever its type.
/// </summary>
/// <remarks>
/// Because it accepts every command, this contract sees the command as
/// <see cref="ICommand"/> and its result as <see cref="object"/>. To work with a typed
/// result, implement <see cref="ICommandPostInterceptor{TCommand, TResult}"/>.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandPostInterceptor: ICommand, IAsyncPostInterceptor<ICommand>;
