using System;
using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.Abstractions.Events;


/// <summary>
/// Event that signals the completion of final interceptor execution in the message pipeline.
/// This event is raised after a final interceptors have been invoked, allowing subscribers
/// to observe or log the final state of the message and its result.
/// </summary>
public class FinishFinalInterceptorInvocationEvent: PipelineEvent
{
    
    /// <summary>
    /// Creates a new instance of <see cref="FinishFinalInterceptorInvocationEvent"/>.
    /// </summary>
    /// <param name="message">The message that was processed.</param>
    /// <param name="result">The final result of the message, if any.</param>
    /// <returns>A new <see cref="FinishFinalInterceptorInvocationEvent"/> instance.</returns>
    public static FinishFinalInterceptorInvocationEvent Create(object message, object? result) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
    };
    
    /// <summary>
    /// Publishes this event to the <see cref="EventHubAccessor"/> for any subscribers.
    /// </summary>
    /// <param name="message">The message that was processed.</param>
    /// <param name="result">The final result of the message after, if any.</param>
    public static void Invoke(object message, object? result)  =>
        EventHubAccessor.Instance.Publish(Create(message, result));
}