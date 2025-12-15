using System;
using System.Collections.Generic;
using System.Linq;

namespace Stella.Ergosfare.Core.Abstractions.SignalHub.Signals;

/// <summary>
/// Signal that signals the beginning of exception interceptors execution in the message pipeline.
/// This event is raised before any exception interceptors are invoked, allowing subscribers
/// to observe or log the start of exception handling for a specific message.
/// </summary>
public sealed class BeginExceptionInterceptingSignal: PipelineSignal
{
    /// <summary>
    /// Gets the total number of exception interceptors that will be executed.
    /// </summary>
    public required ushort InterceptorCount { get; init; }
    
    /// <summary>
    /// Gets the exception that triggered the execution of the exception interceptors.
    /// </summary>
    public required Exception Exception { get; init; }

    
    /// <summary>
    /// Creates a new instance of <see cref="BeginExceptionInterceptingSignal"/>.
    /// </summary>
    /// <param name="message">The message being processed that caused the exception.</param>
    /// <param name="result">The current result of the message pipeline, if any.</param>
    /// <param name="exception">The exception that triggered the interceptors.</param>
    /// <param name="interceptorCount">The total number of exception interceptors to execute.</param>
    /// <returns>A new <see cref="BeginExceptionInterceptingSignal"/> instance.</returns>
    public static BeginExceptionInterceptingSignal Create(object message, object? result, Exception exception, ushort interceptorCount = 0) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        Exception = exception,
        InterceptorCount = interceptorCount,
    };

    /// <summary>
    /// Publishes this event to the <see cref="SignalHubAccessor"/> for any subscribers.
    /// </summary>
    /// <param name="message">The message being processed that caused the exception.</param>
    /// <param name="result">The current result of the message pipeline, if any.</param>
    /// <param name="exception">The exception that triggered the interceptors.</param>
    /// <param name="interceptorCount">The total number of exception interceptors to execute.</param>
    public static void Invoke(object message, object? result, Exception exception, ushort interceptorCount = 0) =>
        SignalHubAccessor.Instance.Publish(Create(message, result, exception, interceptorCount));

    /// <summary>
    /// Returns a sequence of components that uniquely identify this event instance.
    /// Includes the base equality components from <see cref="PipelineSignal"/>, as well as
    /// <see cref="InterceptorCount"/> and <see cref="Exception"/>.
    /// </summary>
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([InterceptorCount, Exception]);
}