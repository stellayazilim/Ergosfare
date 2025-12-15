namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Represents a post-interceptor in the message pipeline that executes after the message handler
/// has produced a result. Allows observing or modifying the result.
/// </summary>
/// <typeparam name="TMessage">The type of message this interceptor handles. Must be non-nullable.</typeparam>
/// <typeparam name="TResult">The type of result produced by the handler. Must be non-nullable.</typeparam>
/// <remarks>
/// This interface extends the non-generic <see cref="IPostInterceptor"/> and casts the untyped inputs
/// to the specified generic types for type-safe handling in implementations.
/// Post-interceptors are invoked after the main handler has run and can modify or inspect the result
/// before it continues through the pipeline.
/// </remarks>
public interface IPostInterceptor<in TMessage,in TResult>
    : IPostInterceptor 
        where TMessage : notnull 
        where TResult : notnull
{

    /// <inheritdoc cref="IPostInterceptor.Handle"/>
    object IPostInterceptor.Handle(object message, object messageResult, IExecutionContext context)
    {
        return Handle((TMessage) message, (TResult) messageResult, context);
    }

    /// <summary>
    /// Handles the message and its result in a type-safe manner after the main handler execution.
    /// </summary>
    /// <param name="message">The message that was processed.</param>
    /// <param name="messageResult">The result produced by the handler. May be null if no result.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>An <see cref="object"/> representing the potentially modified result. 
    /// The returned object will continue through the pipeline.</returns>
    object Handle(TMessage message, TResult messageResult, IExecutionContext context);
}