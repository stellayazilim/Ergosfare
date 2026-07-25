
namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Synchronous exception-interceptor contract for messages of type
/// <typeparamref name="TMessage"/> with results of type <typeparamref name="TResult"/>.
/// Executed when the pipeline throws; may observe the exception and replace the result.
/// </summary>
/// <typeparam name="TMessage">The type of message this interceptor handles.</typeparam>
/// <typeparam name="TResult">The type of result produced by the handler.</typeparam>
/// <remarks>
/// This is a standalone synchronous contract — asynchronous exception interceptors implement
/// <see cref="IAsyncExceptionInterceptor{TMessage}"/> or
/// <see cref="IAsyncExceptionInterceptor{TMessage, TResult}"/> instead; the pipeline
/// dispatches each through its own typed member with no object-typed bridge between them.
/// </remarks>
public interface IExceptionInterceptor<in TMessage, in TResult> : IExceptionInterceptor
    where TMessage : notnull
{
    /// <summary>
    /// Handles an exception thrown while processing the message.
    /// </summary>
    /// <param name="message">The message whose processing threw.</param>
    /// <param name="messageResult">The result produced so far, if any.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// The (possibly replaced) result that continues through the pipeline, or <c>null</c>
    /// to keep the current result.
    /// </returns>
    object? Handle(TMessage message, TResult? messageResult, Exception exception, IExecutionContext context);
}
