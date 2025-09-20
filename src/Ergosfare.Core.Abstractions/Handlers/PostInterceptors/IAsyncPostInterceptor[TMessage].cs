using System.Threading.Tasks;

namespace Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Represents an asynchronous post-interceptor for a specific message type that does not produce a meaningful result.
/// Executes after the main handler has processed the message.
/// </summary>
/// <typeparam name="TMessage">The type of the message being handled.</typeparam>
/// <remarks>
/// This interface allows performing asynchronous side-effects or additional processing after a message has been handled.
/// The <paramref name="_"/> parameter represents the main handler's result but is ignored, since there is no meaningful result.
/// The <see cref="IPostInterceptor{TMessage, object}.Handle"/> implementation delegates to <see cref="HandleAsync"/>.
/// </remarks>
public interface IAsyncPostInterceptor<in TMessage>
    : IPostInterceptor<TMessage, object>
    where TMessage : notnull
{
    /// <inheritdoc cref="IPostInterceptor{TMessage, TResult}.Handle"/>
    object IPostInterceptor<TMessage, object>.Handle(TMessage message, object? _, IExecutionContext context)
    {
        return HandleAsync(message, _, context);
    }
    
    /// <summary>
    /// Handles a message asynchronously after it has been processed by its main handler.
    /// </summary>
    /// <param name="message">The message that was handled by the main handler.</param>
    /// <param name="_">
    /// The result produced by the main handler. For messages without a meaningful result, this can be <c>null</c> and should be ignored.
    /// </param>
    /// <param name="context">The current execution context.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<object> HandleAsync(TMessage message, object? _, IExecutionContext context);
}