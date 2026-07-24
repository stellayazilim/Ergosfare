
namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Synchronous pre-interceptor contract for messages of type <typeparamref name="TMessage"/>.
/// Executed before the main handler; may inspect, validate, enrich, or replace the message.
/// </summary>
/// <typeparam name="TMessage">The type of message this interceptor handles.</typeparam>
/// <remarks>
/// This is a standalone synchronous contract — asynchronous pre-interceptors implement
/// <see cref="IAsyncPreInterceptor{TMessage}"/> instead; the pipeline dispatches each through
/// its own typed member with no object-typed bridge between them.
/// </remarks>
public interface IPreInterceptor<in TMessage> : IPreInterceptor
    where TMessage : notnull
{
    /// <summary>
    /// Handles a message before it reaches the main handler.
    /// </summary>
    /// <param name="message">The input message to process.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// The message that continues through the pipeline — either <paramref name="message"/>
    /// itself or a modified instance of <typeparamref name="TMessage"/>.
    /// </returns>
    object Handle(TMessage message, IExecutionContext context);
}
