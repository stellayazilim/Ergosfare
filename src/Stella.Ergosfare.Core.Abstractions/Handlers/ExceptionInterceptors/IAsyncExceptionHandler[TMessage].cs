using System;
using System.Threading.Tasks;

namespace Stella.Ergosfare.Core.Abstractions.Handlers;

/// <summary>
/// Represents an asynchronous interceptor that handles exceptions thrown during the processing of a specific message type
/// and can modify the resulting message result.
/// </summary>
/// <typeparam name="TMessage">The type of the message being handled.</typeparam>
/// <remarks>
/// Implementations of this interface can inspect the exception, modify the existing result, or produce a new result
/// to continue through the pipeline. This interface is useful for messages that do not have a strongly typed result
/// or when the result type is <c>object</c>.
/// </remarks>
public interface IAsyncExceptionInterceptor<in TMessage>: IExceptionInterceptor<TMessage, object>
    where TMessage : notnull
{
    /// <inheritdoc cref="IExceptionInterceptor{TMessage, TResult}"/>
    object IExceptionInterceptor<TMessage, object>
        .Handle(
            TMessage message, 
            object? messageResult, 
            Exception exception, 
            IExecutionContext context)
    {
        return  HandleAsync(
            message, 
            messageResult, 
            exception, context);
    }
    
    
    /// <summary>
    /// Handles an exception asynchronously for a specific message and its result.
    /// </summary>
    /// <param name="message">The message being processed when the exception occurred.</param>
    /// <param name="messageResult">The current result of the message, if any. Can be modified or replaced.</param>
    /// <param name="exception">The exception that was thrown during message processing.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// The result of the message may be modified and will continue through the pipeline.
    /// </returns>
    Task<object> HandleAsync(
        TMessage message, 
        object? messageResult, 
        Exception exception, 
        IExecutionContext context);
}