using System;
using System.Threading.Tasks;

namespace Ergosfare.Core.Abstractions.Handlers;

/// <summary>
/// Represents an asynchronous interceptor that handles exceptions for a specific message and its strongly typed result.
/// </summary>
/// <typeparam name="TMessage">The type of message being processed. Must be non-nullable.</typeparam>
/// <typeparam name="TResult">The type of the result for the message. Must be non-nullable.</typeparam>
public interface IAsyncExceptionInterceptor<in TMessage, in TResult>: 
    IExceptionInterceptor<TMessage, TResult> 
    where TMessage : notnull
    where TResult : notnull
{
    /// <inheritdoc>
    ///     <cref>IExceptionInterceptor{TMessage, TResult}.Handle</cref>
    /// </inheritdoc>
    object IExceptionInterceptor<TMessage, TResult>.Handle(
        TMessage message,
        TResult? result,
        Exception exception,
        IExecutionContext context)
    {
        return   HandleAsync(message, result, exception, context);
    }
       
    /// <summary>
    /// Handles an exception asynchronously for a specific message and its strongly typed result.
    /// </summary>
    /// <param name="message">The message being processed when the exception occurred.</param>
    /// <param name="result">
    /// The current strongly typed result of the message, if any. 
    /// The interceptor can modify or replace this result, which will continue through the pipeline.
    /// </param>
    /// <param name="exception">The exception that was thrown during message processing.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// The result of type <typeparamref name="TResult"/> may be modified and will be propagated through the pipeline.
    /// </returns>
    Task<object> HandleAsync(TMessage message, TResult? result, Exception exception, IExecutionContext context);
    
}