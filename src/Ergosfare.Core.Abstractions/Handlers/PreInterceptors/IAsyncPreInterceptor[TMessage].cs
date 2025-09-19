using System.Threading.Tasks;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Represents an asynchronous pre-processing interceptor for messages of type <typeparamref name="TMessage"/>.
/// Executed before the main handler processes the message, allowing inspection, validation, or modification of the input.
/// </summary>
/// <typeparam name="TMessage">The type of message this interceptor handles.</typeparam>
/// <remarks>
/// The <see cref="HandleAsync"/> method returns <see cref="object"/> to allow returning a modified version of the input message.
/// - This supports pipelines where messages may be transformed before handling.
/// - Runtime vali
/// </remarks>

public interface IAsyncPreInterceptor<in TMessage>:
    IPreInterceptor<TMessage> 
    where TMessage : notnull
{
    /// <inheritdoc cref="IPreInterceptor{TMessage}.Handle" />
    object IPreInterceptor<TMessage>.Handle(
        TMessage message,  IExecutionContext context) => HandleAsync(message, context);
    
    
    /// <summary>
    /// Handles a message asynchronously before it reaches the main handler.
    /// </summary>
    /// <param name="message">The input message to be processed.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A <see cref="Task{Object}"/> representing the asynchronous operation.
    /// The returned <see cref="object"/> is actually of type <typeparamref name="TMessage"/> (the input message type). 
    /// This allows the interceptor to return either the original message or a modified version, which will continue through the pipeline.
    /// </returns>
    Task<object> HandleAsync(
        TMessage message, 
        IExecutionContext context);
}