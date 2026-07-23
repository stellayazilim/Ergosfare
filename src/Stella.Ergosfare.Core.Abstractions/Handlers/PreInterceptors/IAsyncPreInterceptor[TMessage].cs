using System.Threading.Tasks;

namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Asynchronous pre-interceptor contract for messages of type <typeparamref name="TMessage"/>.
/// Executed before the main handler; may inspect, validate, or replace the message.
/// </summary>
/// <typeparam name="TMessage">The type of message this interceptor handles.</typeparam>
/// <remarks>
/// This is a standalone asynchronous contract — it does not inherit the synchronous
/// <see cref="IPreInterceptor{TMessage}"/>, and there is no object-typed default
/// implementation: the pipeline invokes <see cref="HandleAsync"/> directly.
/// </remarks>
public interface IAsyncPreInterceptor<in TMessage> : IPreInterceptor
    where TMessage : notnull
{
    /// <summary>
    /// Handles a message asynchronously before it reaches the main handler.
    /// </summary>
    /// <param name="message">The input message to be processed.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A <see cref="ValueTask{Object}"/> whose result is the message that continues through
    /// the pipeline — either the original message or a modified instance of
    /// <typeparamref name="TMessage"/>.
    /// </returns>
    ValueTask<object> HandleAsync(TMessage message, IExecutionContext context);
}
