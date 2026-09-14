using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;

/// <summary>
/// Runs once the pipeline of a <typeparamref name="TCommand"/> has settled, reading its
/// result as a <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TCommand">The command type this interceptor accepts.</typeparam>
/// <typeparam name="TResult">The result type the command declares.</typeparam>
/// <remarks>
/// It runs after the pre-, post- and exception stages and sees the command, the result and
/// any failure, but cannot change the outcome. A pipeline stopped by <c>context.Abort()</c>
/// runs no final interceptors.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandFinalInterceptor<in TCommand,in TResult> :
    ICommand, IAsyncFinalInterceptor<TCommand, TResult>
    where TCommand : ICommand<TResult>;
