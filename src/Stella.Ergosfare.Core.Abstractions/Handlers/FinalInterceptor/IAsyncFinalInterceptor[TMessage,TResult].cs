using System;
using System.Threading.Tasks;

namespace Stella.Ergosfare.Core.Abstractions.Handlers;

/// <summary>
/// Asynchronous final-interceptor contract for messages of type <typeparamref name="TMessage"/>
/// with strongly typed results of type <typeparamref name="TResult"/>. Always executed at the
/// end of the pipeline, regardless of success or failure — for cleanup, auditing, or logging.
/// </summary>
/// <typeparam name="TMessage">The type of message this interceptor handles.</typeparam>
/// <typeparam name="TResult">The type of result produced by the handler.</typeparam>
/// <remarks>
/// This is a standalone asynchronous contract — it does not inherit the synchronous
/// <see cref="IFinalInterceptor{TMessage, TResult}"/>, and there is no object-typed default
/// implementation: the pipeline invokes <see cref="HandleAsync"/> directly.
/// </remarks>
public interface IAsyncFinalInterceptor<in TMessage, in TResult> : IFinalInterceptor
    where TMessage : notnull
{
    /// <summary>
    /// Handles the end of the pipeline for the given message.
    /// </summary>
    /// <param name="message">The message that was processed.</param>
    /// <param name="result">The final result, if any.</param>
    /// <param name="exception">The exception that terminated the pipeline, if any.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask HandleAsync(TMessage message, TResult? result, Exception? exception, IExecutionContext context);
}
