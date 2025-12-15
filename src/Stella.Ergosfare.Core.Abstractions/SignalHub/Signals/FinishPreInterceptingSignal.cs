using System;

namespace Stella.Ergosfare.Core.Abstractions.SignalHub.Signals;

/// <summary>
/// Represents a signal published after a pre-interceptor has finished execution for a message.
/// </summary>
public sealed class FinishPreInterceptingSignal: PipelineSignal
{
    /// <summary>
    /// Creates a new instance of <see cref="FinishPreInterceptingSignal"/> for the specified message and result.
    /// </summary>
    /// <param name="message">The message that was processed.</param>
    /// <param name="result">The result of the message processing, if any.</param>
    /// <returns>A new <see cref="FinishPreInterceptingSignal"/> instance.</returns>
    public static FinishPreInterceptingSignal Create(object message, object? result) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result
    }; 

    
    /// <summary>
    /// Publishes a new <see cref="FinishPreInterceptingSignal"/> to the signal hub.
    /// </summary>
    /// <param name="message">The message that was processed.</param>
    /// <param name="result">The result of the message processing, if any.</param>
    public static void Invoke(object message, object? result) => 
        SignalHubAccessor.Instance.Publish(Create(message, result));
}