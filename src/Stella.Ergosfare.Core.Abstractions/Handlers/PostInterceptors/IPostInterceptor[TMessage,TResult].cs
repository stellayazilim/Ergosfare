
namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Synchronous post-interceptor contract for messages of type <typeparamref name="TMessage"/>
/// producing results of type <typeparamref name="TResult"/>. Executes after the main handler
/// and may observe or replace the result.
/// </summary>
/// <typeparam name="TMessage">The type of message this interceptor handles. Must be non-nullable.</typeparam>
/// <typeparam name="TResult">The type of result produced by the handler. Must be non-nullable.</typeparam>
/// <remarks>
/// This is a standalone synchronous contract — asynchronous post-interceptors implement
/// <see cref="IAsyncPostInterceptor{TMessage}"/> or
/// <see cref="IAsyncPostInterceptor{TMessage, TResult}"/> instead; the pipeline dispatches
/// each through its own typed member with no object-typed bridge between them.
/// </remarks>
public interface IPostInterceptor<in TMessage, in TResult>
    : IPostInterceptor
        where TMessage : notnull
        where TResult : notnull
{
    /// <summary>
    /// Handles a message after it has been processed by the main handler.
    /// </summary>
    /// <param name="message">The message that was handled by the main handler.</param>
    /// <param name="messageResult">The result produced by the main handler.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// The (possibly replaced) result that continues through the pipeline.
    /// </returns>
    object Handle(TMessage message, TResult messageResult, IExecutionContext context);
}
