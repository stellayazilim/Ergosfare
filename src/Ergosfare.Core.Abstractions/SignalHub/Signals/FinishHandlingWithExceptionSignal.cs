using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;

/// <summary>
/// Represents a signal published after a message has finished handling with an exception.
/// </summary>
public sealed class FinishHandlingWithExceptionSignal: PipelineSignal
{
    /// <summary>
    /// Gets or sets the exception that occurred during message handling.
    /// </summary>
    public required Exception Exception { get; init; }
    
    
    /// <summary>
    /// Creates a new instance of <see cref="FinishHandlingWithExceptionSignal"/> with the specified message, result, and exception.
    /// </summary>
    /// <param name="message">The message that was handled.</param>
    /// <param name="result">The result of the message handling, if any.</param>
    /// <param name="exception">The exception that occurred during handling.</param>
    /// <returns>A new <see cref="FinishHandlingWithExceptionSignal"/> instance.</returns>
    public static FinishHandlingWithExceptionSignal Create(object message, object? result, Exception exception) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        Exception = exception,
    };
    
    
    /// <summary>
    /// Publishes a new <see cref="FinishHandlingWithExceptionSignal"/> to the signal hub.
    /// </summary>
    /// <param name="message">The message that was handled.</param>
    /// <param name="result">The result of the message handling, if any.</param>
    /// <param name="exception">The exception that occurred during handling.</param>
    public static void Invoke(object message, object? result, Exception exception) =>  
        SignalHubAccessor.Instance.Publish(Create(message, result, exception));

    
    /// <inheritdoc />
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception]);
}