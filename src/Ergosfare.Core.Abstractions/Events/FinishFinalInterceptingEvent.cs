using System;
using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.Abstractions.Events;


/// <summary>
/// Event that signals the completion of the entire final interceptor phase in the message pipeline.
/// This event is raised once all final interceptors have run, indicating that the message has
/// finished its final stage processing and is ready for post-pipeline actions or logging.
/// </summary>
public class FinishFinalInterceptingEvent: PipelineEvent
{
    /// <summary>
    /// Creates a new instance of <see cref="FinishFinalInterceptingEvent"/>.
    /// </summary>
    /// <param name="message">The message that was processed by the pipeline.</param>
    /// <param name="result">The final result of the message after all final interceptors, if any.</param>
    /// <returns>A new <see cref="FinishFinalInterceptingEvent"/> instance.</returns>
    public static FinishFinalInterceptingEvent Create(object message, object? result) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
    };
    
    /// <summary>
    /// Publishes this event to the <see cref="EventHubAccessor"/> so that any subscribers
    /// can react to the completion of the final interceptor phase.
    /// </summary>
    /// <param name="message">The message that was processed.</param>
    /// <param name="result">The final result of the message after all final interceptors, if any.</param>
    public static void Invoke(object message, object? result)  =>
        EventHubAccessor.Instance.Publish(Create(message, result));

}