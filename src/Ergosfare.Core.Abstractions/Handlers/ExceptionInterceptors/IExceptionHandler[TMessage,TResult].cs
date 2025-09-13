using System;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Represents an interceptor that handles exceptions thrown during the processing of a specific message type
/// and can modify the resulting message result.
/// </summary>
/// <typeparam name="TMessage">The type of the message being handled.</typeparam>
/// <typeparam name="TResult">The type of the message result.</typeparam>
/// <remarks>
/// Implementations of this interface can inspect the exception, modify the existing result, or produce a new result
/// to continue through the pipeline. This design allows support for both legacy and fluent-result styles, 
/// making it flexible for pipelines where the concrete result type may only be known at runtime.
/// </remarks>
public interface IExceptionInterceptor<in TMessage, in TResult> : IExceptionInterceptor
    where TMessage : notnull
{
    /// <inheritdoc cref="IExceptionInterceptor.Handle"/>
    object IExceptionInterceptor.Handle(object message, object? messageResult, Exception exception, IExecutionContext context)
    {
        return Handle((TMessage) message, (TResult?) messageResult, exception,  context);
    }
    
    
    /// <summary>
    /// Handles an exception for a specific message and its result.
    /// </summary>
    /// <param name="message">The message being processed when the exception occurred.</param>
    /// <param name="messageResult">The current result of the message, if any. Can be modified or replaced.</param>
    /// <param name="exception">The exception that was thrown during message processing.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// Returns the modified or original message result as an <see cref="object"/>.
    /// The returned result will continue through the pipeline and can be further processed by other interceptors or handlers.
    /// </returns>
    object Handle(TMessage message,  TResult? messageResult, Exception exception,  IExecutionContext context);

}