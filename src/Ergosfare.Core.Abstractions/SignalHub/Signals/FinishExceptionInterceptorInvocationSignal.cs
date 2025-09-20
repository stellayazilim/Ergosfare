using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;

/// <summary>
/// Represents a signal published after an exception interceptor has finished executing in the pipeline.
/// </summary>
public sealed class FinishExceptionInterceptorInvocationSignal: PipelineSignal
{
    /// <summary>
    /// Gets or sets the exception that was handled by the interceptor.
    /// </summary>
    public required Exception Exception { get; init; }
 
    
    /// <summary>
    /// Creates a new instance of <see cref="FinishExceptionInterceptorInvocationSignal"/> with the specified message, result, and exception.
    /// </summary>
    /// <param name="message">The message being processed in the pipeline.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <param name="exception">The exception handled by the interceptor.</param>
    /// <returns>A new <see cref="FinishExceptionInterceptorInvocationSignal"/> instance.</returns>
    public static FinishExceptionInterceptorInvocationSignal Create(object message, object? result, Exception exception) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        Exception = exception
    };
    
    
    /// <summary>
    /// Publishes a new <see cref="FinishExceptionInterceptorInvocationSignal"/> to the signal hub.
    /// </summary>
    /// <param name="message">The message being processed in the pipeline.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <param name="exception">The exception handled by the interceptor.</param>
    public static void Invoke(object message, object? result, Exception exception) => 
        SignalHubAccessor.Instance.Publish(Create(message, result, exception));
    
    
    /// <summary>
    /// Returns the components used to determine equality for this signal.
    /// </summary>
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception]);
}