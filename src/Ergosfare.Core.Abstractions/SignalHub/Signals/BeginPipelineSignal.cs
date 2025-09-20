using System;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;

/// <summary>
///     Represents a signal that is published at the very beginning of a pipeline execution.
/// </summary>
public sealed class BeginPipelineSignal : PipelineSignal
{
    /// <summary>
    ///     Creates a new instance of <see cref="BeginPipelineSignal"/> with the specified message and result.
    /// </summary>
    /// <param name="message">The message that is being processed in the pipeline.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <returns>A new <see cref="BeginPipelineSignal"/> instance.</returns>
    public static BeginPipelineSignal Create(object message, object? result) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
    }; 
        
    
    /// <summary>
    ///     Publishes a new <see cref="BeginPipelineSignal"/> to the signal hub.
    /// </summary>
    /// <param name="message">The message that is being processed in the pipeline.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    public static void Invoke(object message, object? result) => 
        SignalHubAccessor.Instance.Publish(Create(message, result));
}