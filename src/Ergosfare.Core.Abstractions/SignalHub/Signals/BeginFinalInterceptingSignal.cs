using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;

/// <summary>
/// Event that signals the beginning of final interceptors execution in the message pipeline.
/// This event is raised before any final interceptors are invoked, allowing subscribers
/// to observe or log the start of final interceptor processing for a specific message.
/// </summary>
public sealed class BeginFinalInterceptingSignal: PipelineSignal
{
    /// <summary>
    /// Gets the total number of final interceptors that will be executed.
    /// </summary>
    public required ushort InterceptorCount { get; init; }
    
    
    /// <summary>
    /// Gets the exception, if any, that was raised during the message pipeline.
    /// This may be <c>null</c> if no exception occurred.
    /// </summary>
    public Exception? Exception { get; init; }
    
    /// <summary>
    /// Creates a new instance of <see cref="BeginFinalInterceptingSignal"/>.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The current result of the message pipeline, if any.</param>
    /// <param name="exception">The exception that occurred during processing, if any.</param>
    /// <param name="interceptorCount">The total number of final interceptors to execute.</param>
    /// <returns>A new <see cref="BeginFinalInterceptingSignal"/> instance.</returns>
    public static BeginFinalInterceptingSignal Create(object message, object? result, Exception? exception, ushort interceptorCount = 0) => 
        new()
        {
            Message = message ?? throw new ArgumentNullException(nameof(message)),
            Result = result,
            Exception = exception,
            InterceptorCount = interceptorCount
        };
    
    /// <summary>
    /// Publishes this event to the <see cref="SignalHubAccessor"/> for any subscribers.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The current result of the message pipeline, if any.</param>
    /// <param name="exception">The exception that occurred during processing, if any.</param>
    /// <param name="interceptorCount">The total number of final interceptors to execute.</param>
    public static void Invoke(object message, object? result, Exception? exception, ushort interceptorCount = 0) =>
        SignalHubAccessor.Instance.Publish(Create(message, result, exception, interceptorCount));
    
    
    /// <summary>
    /// Returns a sequence of components that uniquely identify this event instance.
    /// Includes the base equality components from <see cref="PipelineSignal"/>, as well as
    /// <see cref="Exception"/> and <see cref="InterceptorCount"/>.
    /// </summary>
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception ?? null!, InterceptorCount]);
}