using System;
using System.Threading.Tasks;

namespace Stella.Ergosfare.Core.Abstractions.Handlers;

/// <summary>
/// Asynchronous exception-interceptor contract for messages of type
/// <typeparamref name="TMessage"/> that is agnostic of the result type.
/// Executed when the pipeline throws; may observe the exception and replace the result.
/// </summary>
/// <typeparam name="TMessage">The type of message this interceptor handles.</typeparam>
/// <remarks>
/// This is a standalone asynchronous contract — it does not inherit the synchronous
/// <see cref="IExceptionInterceptor{TMessage, TResult}"/>, and there is no object-typed
/// default implementation: the pipeline invokes <see cref="HandleAsync"/> directly.
/// </remarks>
public interface IAsyncExceptionInterceptor<in TMessage> : IExceptionInterceptor
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
    /// A <see cref="ValueTask{Object}"/> whose result is the (possibly replaced) result that
    /// continues through the pipeline.
    /// </returns>
    ValueTask<object> HandleAsync(
        TMessage message,
        object? messageResult,
        Exception exception,
        IExecutionContext context);
}
