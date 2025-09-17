using System;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;


/// <summary>
/// Event that signals the completion of the entire final interceptor phase in the message pipeline.
/// This event is raised once all final interceptors have run, indicating that the message has
/// finished its final stage processing and is ready for post-pipeline actions or logging.
/// </summary>
public class FinishFinalInterceptingSignal: PipelineSignal
{
    /// <summary>
    /// Creates a new instance of <see cref="FinishFinalInterceptingSignal"/>.
    /// </summary>
    /// <param name="message">The message that was processed by the pipeline.</param>
    /// <param name="result">The final result of the message after all final interceptors, if any.</param>
    /// <returns>A new <see cref="FinishFinalInterceptingSignal"/> instance.</returns>
    public static FinishFinalInterceptingSignal Create(object message, object? result) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
    };
    
    /// <summary>
    /// Publishes this event to the <see cref="SignalHubAccessor"/> so that any subscribers
    /// can react to the completion of the final interceptor phase.
    /// </summary>
    /// <param name="message">The message that was processed.</param>
    /// <param name="result">The final result of the message after all final interceptors, if any.</param>
    public static void Invoke(object message, object? result)  =>
        SignalHubAccessor.Instance.Publish(Create(message, result));

}