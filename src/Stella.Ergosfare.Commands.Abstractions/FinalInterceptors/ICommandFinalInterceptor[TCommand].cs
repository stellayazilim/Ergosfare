using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;

/// <summary>
/// Runs once the pipeline of a <typeparamref name="TCommand"/> has settled, whether it
/// succeeded or failed.
/// </summary>
/// <typeparam name="TCommand">The command type this interceptor accepts.</typeparam>
/// <remarks>
/// It runs after the pre-, post- and exception stages and sees the command, the result and
/// any failure, but cannot change the outcome. A pipeline stopped by <c>context.Abort()</c>
/// runs no final interceptors. Use it for logging, cleanup and other last steps.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandFinalInterceptor<in TCommand> :ICommand, IAsyncFinalInterceptor<TCommand>
    where TCommand : ICommand;
