using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Runs once the pipeline of any command has settled, whatever its type.
/// </summary>
/// <remarks>
/// Use this for work that applies across command types — logging, metrics, cleanup. It
/// observes the outcome and cannot change it, and a pipeline stopped by
/// <c>context.Abort()</c> runs no final interceptors. For a typed command or result,
/// implement <see cref="ICommandFinalInterceptor{TCommand}"/> or
/// <see cref="ICommandFinalInterceptor{TCommand, TResult}"/>.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandFinalInterceptor: ICommand, IAsyncFinalInterceptor<ICommand>;
