using System;

namespace Stella.Ergosfare.Core.Abstractions.SignalHub.Signals;


/// <summary>
/// Event that signals the completion of final interceptor execution in the message pipeline.
/// This event is raised after a final interceptors have been invoked, allowing subscribers
/// to observe or log the final state of the message and its result.
/// </summary>
public class FinishFinalInterceptorInvocationSignal: PipelineSignal
{
    
    /// <summary>
    /// Creates a new instance of <see cref="FinishFinalInterceptorInvocationSignal"/>.
    /// </summary>
    /// <param name="message">The message that was processed.</param>
    /// <param name="result">The final result of the message, if any.</param>
    /// <returns>A new <see cref="FinishFinalInterceptorInvocationSignal"/> instance.</returns>
    public static FinishFinalInterceptorInvocationSignal Create(object message, object? result) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
    };
    
    /// <summary>
    /// Publishes this event to the <see cref="SignalHubAccessor"/> for any subscribers.
    /// </summary>
    /// <param name="message">The message that was processed.</param>
    /// <param name="result">The final result of the message after, if any.</param>
    public static void Invoke(object message, object? result)  =>
        SignalHubAccessor.Instance.Publish(Create(message, result));
}