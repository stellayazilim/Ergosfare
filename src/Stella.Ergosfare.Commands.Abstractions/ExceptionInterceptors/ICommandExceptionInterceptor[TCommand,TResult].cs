using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Handles failures raised while dispatching a <typeparamref name="TCommand"/> and supplies
/// the result the caller receives instead.
/// </summary>
/// <typeparam name="TCommand">The command type this interceptor accepts.</typeparam>
/// <typeparam name="TResult">The result type the command declares.</typeparam>
/// <remarks>
/// <typeparamref name="TCommand"/> is contravariant, so an interceptor written against a
/// base command type also runs for the commands derived from it;
/// <typeparamref name="TResult"/> stays invariant because it is returned. Add
/// <see cref="IExceptionInterceptorFilter{TException}"/> — or implement
/// <see cref="ICommandExceptionInterceptorFor{TCommand, TResult, TException}"/> — to accept
/// only certain failures.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandExceptionInterceptor<in TCommand, TResult> : ICommand, IAsyncExceptionInterceptor<TCommand, TResult>
    where TCommand : ICommand<TResult>
    where TResult : notnull
{
    /// <summary>
    /// Forwards the core contract to the typed method below.
    /// </summary>
    /// <param name="command">The command whose dispatch failed.</param>
    /// <param name="result">The result produced so far, if any.</param>
    /// <param name="exception">The failure being handled.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>The result the typed method returned.</returns>
    async ValueTask<object?> IAsyncExceptionInterceptor<TCommand, TResult>.HandleAsync(
        TCommand command, TResult? result, Exception exception, ErgosfareContext context)
        => await HandleAsync(command, result, exception, context);

    /// <summary>
    /// Handles <paramref name="exception"/> and produces the result to continue with.
    /// </summary>
    /// <param name="command">The command whose dispatch failed.</param>
    /// <param name="result">
    /// The result produced before the failure, which is the result type's default when the
    /// handler itself failed.
    /// </param>
    /// <param name="exception">The failure being handled.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>The result the caller receives.</returns>
    /// <remarks>
    /// Running this method is what marks the failure handled, and a handled failure has to
    /// leave a result behind: the call site locked the result type when it dispatched, so
    /// nothing may answer it with nothing. To leave a failure for the caller, do not accept
    /// it — a failure no interceptor accepts reaches the caller unchanged. Where absence is
    /// a legitimate answer, express it in the result type, as <c>Result&lt;T&gt;</c> does.
    /// </remarks>
    new ValueTask<TResult> HandleAsync(TCommand command, TResult? result, Exception exception, ErgosfareContext context);
}
