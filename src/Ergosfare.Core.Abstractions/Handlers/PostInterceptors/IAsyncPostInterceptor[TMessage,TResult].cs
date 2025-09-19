using System.Threading.Tasks;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Represents an asynchronous post-interceptor for a specific message type and its result type.
/// Executes after the main handler has processed the message and allows inspecting or modifying the result.
/// </summary>
/// <typeparam name="TMessage">The type of the message being handled.</typeparam>
/// <typeparam name="TResult">The type of the result produced by the main handler.</typeparam>
/// <remarks>
/// This interface allows performing asynchronous operations after a message has been handled. 
/// The interceptor can inspect or modify the <typeparamref name="TResult"/> value.
/// 
/// The <see cref="IPostInterceptor{TMessage, TResult}.Handle"/> implementation delegates to <see cref="HandleAsync"/>.
/// Returning <c>null</c> is allowed only if it matches the intended pipeline behavior; otherwise, a meaningful object should be returned.
/// </remarks>
public interface IAsyncPostInterceptor<in TMessage, in TResult>
    :IPostInterceptor<TMessage, TResult>
    where TMessage : notnull 
    where TResult: notnull
{
    /// <inheritdoc cref="IPostInterceptor{TMessage, TResult}.Handle"/>
    object IPostInterceptor<TMessage, TResult>.Handle(
        TMessage message, 
        TResult? messageResult, 
        IExecutionContext context)
    {
        return HandleAsync(message,  messageResult, AmbientExecutionContext.Current);
    }
    
    /// <inheritdoc cref="IPostInterceptor{TMessage,TResult}.Handle"/> 
    /// <summary>
    /// Handles a message asynchronously after it has been processed by the main handler.
    /// </summary>
    /// <param name="message">The message that was handled by the main handler.</param>
    /// <param name="messageResult">The result produced by the main handler, which can be inspected or modified.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A <see cref="Task{Object}"/> representing the asynchronous operation.
    /// The returned object should represent the modified result to continue through the pipeline.
    /// </returns>
    Task<object> HandleAsync(TMessage message, TResult? messageResult, IExecutionContext context);
}

