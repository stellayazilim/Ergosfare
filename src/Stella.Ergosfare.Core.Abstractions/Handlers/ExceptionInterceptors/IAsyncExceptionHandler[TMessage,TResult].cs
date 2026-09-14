
namespace Stella.Ergosfare.Core.Abstractions.Handlers;

/// <summary>
/// Handles a failure raised while dispatching a <typeparamref name="TMessage"/>
/// asynchronously, and supplies the result the caller receives instead.
/// </summary>
/// <typeparam name="TMessage">The message type this interceptor accepts.</typeparam>
/// <typeparam name="TResult">The result type this interceptor accepts.</typeparam>
/// <remarks>
/// Running is what marks the failure handled: once any exception interceptor runs, the
/// dispatch returns a result rather than throwing. This is a contract in its own right; it
/// does not extend the synchronous <see cref="IExceptionInterceptor{TMessage, TResult}"/>.
/// </remarks>
public interface IAsyncExceptionInterceptor<in TMessage, in TResult> :
    IExceptionInterceptor
    where TMessage : notnull
{
    /// <summary>
    /// Handles <paramref name="exception"/> and produces the result to continue with.
    /// </summary>
    /// <param name="message">The message whose dispatch failed.</param>
    /// <param name="result">
    /// The result produced so far, which is the result type's default when the main handler
    /// itself failed.
    /// </param>
    /// <param name="exception">The failure being handled.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>
    /// The result that continues through the pipeline. This value replaces the current
    /// result outright — producing <c>null</c> makes the result <c>null</c> rather than
    /// preserving what came before.
    /// </returns>
    ValueTask<object?> HandleAsync(TMessage message, TResult? result, Exception exception, ErgosfareContext context);
}
