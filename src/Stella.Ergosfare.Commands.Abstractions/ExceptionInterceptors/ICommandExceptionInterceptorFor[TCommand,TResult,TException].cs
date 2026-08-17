using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Handles failures of type <typeparamref name="TException"/> raised while dispatching a
/// <typeparamref name="TCommand"/>, and supplies the result the caller receives instead.
/// </summary>
/// <typeparam name="TCommand">The command type this interceptor accepts.</typeparam>
/// <typeparam name="TResult">The result type the command declares.</typeparam>
/// <typeparam name="TException">
/// The failure type this interceptor accepts. Matching follows <c>catch</c> semantics, so
/// derived types match too.
/// </typeparam>
/// <remarks>
/// The failure arrives already typed, so no type test is needed in the body. Filtering
/// changes nothing about order: the interceptors that accept a failure run in the stage's
/// usual order, by descending weight and then type name, threading the result through each.
/// A failure no interceptor accepts reaches the caller with its original stack.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandExceptionInterceptorFor<in TCommand, TResult, TException> :
    ICommand, IAsyncExceptionInterceptor<TCommand, TResult>, IExceptionInterceptorFilter<TException>
    where TCommand : ICommand<TResult>
    where TResult : notnull
    where TException : Exception
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
        // The cast is safe: the stage only runs this interceptor once its filter accepted
        // the failure.
        => await HandleAsync(command, result, (TException)exception, context);

    /// <summary>
    /// Handles <paramref name="exception"/> and produces the result to continue with.
    /// </summary>
    /// <param name="command">The command whose dispatch failed.</param>
    /// <param name="result">
    /// The result produced before the failure, which is the result type's default when the
    /// handler itself failed.
    /// </param>
    /// <param name="exception">The failure being handled, already typed.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>The result the caller receives.</returns>
    /// <remarks>
    /// Running this method is what marks the failure handled, and a handled failure has to
    /// leave a result behind: the call site locked the result type when it dispatched. To
    /// leave a failure for the caller, do not accept it — narrow
    /// <typeparamref name="TException"/> so this interceptor never sees it, and a failure no
    /// interceptor accepts reaches the caller unchanged.
    /// </remarks>
    ValueTask<TResult> HandleAsync(TCommand command, TResult? result, TException exception, ErgosfareContext context);
}
