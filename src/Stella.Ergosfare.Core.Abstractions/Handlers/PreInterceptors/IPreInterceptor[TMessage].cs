namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Represents a pre-processing interceptor for messages of type <typeparamref name="TMessage"/>.
/// Executed before the main handler processes the message, allowing inspection, validation, enrichment,
/// or even rejection of the input message.
/// </summary>
/// <typeparam name="TMessage">The type of message this interceptor handles.</typeparam>
/// <remarks>
/// <inheritdoc />
/// <para>
/// The <see cref="Handle"/> method returns <see cref="object"/> instead of <typeparamref name="TMessage"/> 
/// to support scenarios where the input message can be transformed or replaced before passing to the handler.
/// This provides flexibility for pipelines with runtime-typed messages, while maintaining type safety
/// when the pipeline is executed.
/// </para>
/// <para>
/// Use <see cref="AmbientExecutionContext.Current"/> when accessing ambient or contextual information
/// during message processing.
/// </para>
/// </remarks>
public interface IPreInterceptor<in TMessage> : IPreInterceptor 
    where TMessage : notnull
{
    /// <inheritdoc cref="IPreInterceptor.Handle" />
    object IPreInterceptor.Handle(object message,  IExecutionContext context)
    {
        return Handle((TMessage)message, context);
    }
    
    
    /// <summary>
    /// Handles a message before it reaches the main handler.
    /// </summary>
    /// <param name="message">The input message to process.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// Returns the original or a modified message as <see cref="object"/> to allow pipeline transformations.
    /// </returns>
    object Handle(TMessage message, IExecutionContext context);
}


