using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;

/// <summary>
/// Represents a signal that is published at the end of exception intercepting in the pipeline.
/// </summary>
public sealed class FinishExceptionInterceptingSignal: PipelineSignal
{
    /// <summary>
    /// Gets or sets the exception that was intercepted.
    /// </summary>
    public required Exception Exception { get; init; }
    
    
    /// <summary>
    /// Creates a new instance of <see cref="FinishExceptionInterceptingSignal"/> with the specified message, result, and exception.
    /// </summary>
    /// <param name="message">The message being processed in the pipeline.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <param name="exception">The exception that was intercepted.</param>
    /// <returns>A new <see cref="FinishExceptionInterceptingSignal"/> instance.</returns>
    public static FinishExceptionInterceptingSignal Create(object message, object? result, Exception exception) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        Exception = exception ?? throw new ArgumentNullException(nameof(exception))
    };
    
    
    /// <summary>
    /// Publishes a new <see cref="FinishExceptionInterceptingSignal"/> to the signal hub.
    /// </summary>
    /// <param name="message">The message being processed in the pipeline.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <param name="exception">The exception that was intercepted.</param>
    public static void Invoke(object message, object? result, Exception exception) =>
        SignalHubAccessor.Instance.Publish(Create(message, result, exception));
    
    
    /// <summary>
    /// Returns the components used to determine equality for this signal.
    /// </summary>
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception]);
}