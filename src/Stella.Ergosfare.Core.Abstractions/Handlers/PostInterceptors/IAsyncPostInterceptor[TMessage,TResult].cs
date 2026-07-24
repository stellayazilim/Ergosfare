
namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Asynchronous post-interceptor contract for messages of type <typeparamref name="TMessage"/>
/// producing results of type <typeparamref name="TResult"/>. Executes after the main handler
/// and may observe or replace the result.
/// </summary>
/// <typeparam name="TMessage">The type of the message being handled.</typeparam>
/// <typeparam name="TResult">The type of result produced by the handler.</typeparam>
/// <remarks>
/// This is a standalone asynchronous contract — it does not inherit the synchronous
/// <see cref="IPostInterceptor{TMessage, TResult}"/>, and there is no object-typed default
/// implementation: the pipeline invokes <see cref="HandleAsync"/> directly.
/// </remarks>
public interface IAsyncPostInterceptor<in TMessage, in TResult>
    : IPostInterceptor
    where TMessage : notnull
    where TResult : notnull
{
    /// <summary>
    /// Handles a message asynchronously after it has been processed by the main handler.
    /// </summary>
    /// <param name="message">The message that was handled by the main handler.</param>
    /// <param name="messageResult">The result produced by the main handler.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A <see cref="ValueTask{Object}"/> whose result is the (possibly replaced) result that
    /// continues through the pipeline.
    /// </returns>
    ValueTask<object> HandleAsync(TMessage message, TResult messageResult, IExecutionContext context);
}
