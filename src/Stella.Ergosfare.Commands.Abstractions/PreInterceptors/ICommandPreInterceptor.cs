using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Runs before the handler of any command, whatever its type.
/// </summary>
/// <remarks>
/// Because it accepts every command, this contract sees them as <see cref="ICommand"/> and
/// returns <see cref="object"/>. To work with one command type without casting — and to
/// return that type rather than <see cref="object"/> — implement
/// <see cref="ICommandPreInterceptor{TCommand}"/>.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandPreInterceptor: ICommand, IAsyncPreInterceptor<ICommand>;
