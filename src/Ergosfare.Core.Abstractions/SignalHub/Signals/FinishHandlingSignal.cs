using System;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;

/// <summary>
/// Represents a signal published after a message has finished being handled by main handler in the pipeline.
/// </summary>
public sealed class FinishHandlingSignal: PipelineSignal
{
    /// <summary>
    /// Creates a new instance of <see cref="FinishHandlingSignal"/> with the specified message and result.
    /// </summary>
    /// <param name="message">The message that was handled.</param>
    /// <param name="result">The result of the message handling, if any.</param>
    /// <returns>A new <see cref="FinishHandlingSignal"/> instance.</returns>
    public static FinishHandlingSignal Create(object message, object? result) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result
    }; 

    
    /// <summary>
    /// Publishes a new <see cref="FinishHandlingSignal"/> to the signal hub.
    /// </summary>
    /// <param name="message">The message that was handled.</param>
    /// <param name="result">The result of the message handling, if any.</param>
    public static void Invoke(object message, object? result) => 
        SignalHubAccessor.Instance.Publish(Create(message, result));
}