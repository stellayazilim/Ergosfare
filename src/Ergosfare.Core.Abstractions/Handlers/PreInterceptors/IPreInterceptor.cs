namespace Ergosfare.Core.Abstractions.Handlers;

/// <summary>
/// Represents a non-generic pre-processing interceptor for messages.
/// </summary>
/// <remarks>
/// Pre-interceptors are executed before the main handler processes a message. 
/// They allow you to inspect, validate, enrich, or replace the message before it reaches the handler.
/// </remarks>
public interface IPreInterceptor
{
    
    /// <summary>
    /// Handles a message before it is processed by its main handler.
    /// </summary>
    /// <param name="message">The message to be processed. Can be modified or replaced by the interceptor.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// Returns the original or modified message as <see cref="object"/>. 
    /// Using <see cref="object"/> allows runtime flexibility for pipelines with unknown concrete message types.
    /// </returns>
    object Handle(object message, IExecutionContext context) ;
}