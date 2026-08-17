
namespace Stella.Ergosfare.Core.Abstractions.Handlers;

/// <summary>
/// Handles a failure raised while dispatching a <typeparamref name="TMessage"/>
/// asynchronously, without naming the result type.
/// </summary>
/// <typeparam name="TMessage">The message type this interceptor accepts.</typeparam>
/// <remarks>
/// Running is what marks the failure handled: once any exception interceptor runs, the
/// dispatch returns a result rather than throwing. To read a typed result, implement
/// <see cref="IAsyncExceptionInterceptor{TMessage, TResult}"/> instead; these are separate
/// contracts and an interceptor implements one of them.
/// </remarks>
public interface IAsyncExceptionInterceptor<in TMessage> : IExceptionInterceptor
    where TMessage : notnull
{
    /// <summary>
    /// Handles <paramref name="exception"/> and produces the result to continue with.
    /// </summary>
    /// <param name="message">The message whose dispatch failed.</param>
    /// <param name="messageResult">
    /// The result produced so far, which is <c>null</c> when the main handler itself failed.
    /// </param>
    /// <param name="exception">The failure being handled.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>
    /// The result that continues through the pipeline, which must be of the pipeline's
    /// result type. This value replaces the current result outright.
    /// </returns>
    ValueTask<object> HandleAsync(
        TMessage message,
        object? messageResult,
        Exception exception,
        ErgosfareContext context);
}
