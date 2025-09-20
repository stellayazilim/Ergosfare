using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;
/// <summary>
/// Event that signals the start of an individual final interceptor invocation in the pipeline.
/// Raised before a specific final interceptor runs, allowing subscribers to track or log
/// the processing of each final interceptor for a message.
/// </summary>
public class BeginFinalInterceptorInvocationSignal: PipelineSignal
{
    /// <summary>
    /// The <see cref="Type"/> of the final interceptor that is about to be invoked.
    /// </summary>
    public required Type InterceptorType { get; init; }
    
    /// <summary>
    /// Optional exception that may have occurred prior to this final interceptor invocation.
    /// </summary>
    public Exception? Exception { get; init; }
    
    
    /// <summary>
    /// Creates a new instance of <see cref="BeginFinalInterceptorInvocationSignal"/>.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The current result of the message, if any.</param>
    /// <param name="exception">An optional exception captured earlier in the pipeline.</param>
    /// <param name="interceptorType">The type of the final interceptor that is about to execute.</param>
    /// <returns>A new <see cref="BeginFinalInterceptorInvocationSignal"/> instance.</returns>
    public static BeginFinalInterceptorInvocationSignal Create(object message, object? result, Exception? exception, Type interceptorType) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        InterceptorType = interceptorType ?? throw new ArgumentNullException(nameof(interceptorType)),
        Exception = exception
    };
    
    
    /// <summary>
    /// Publishes this event to the <see cref="SignalHubAccessor"/> for subscribers to track
    /// the start of a final interceptor invocation.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The current result of the message, if any.</param>
    /// <param name="exception">An optional exception captured earlier in the pipeline.</param>
    /// <param name="handlerType">The type of the final interceptor that is about to execute.</param>
    public static void Invoke(object message, object? result, Exception? exception, Type handlerType) =>
        SignalHubAccessor.Instance.Publish(Create(message, result, exception, handlerType));
    
    
    /// <summary>
    /// Returns components that define equality for this event instance.
    /// Includes the base equality components, the handler type, and any attached exception.
    /// </summary>
    /// <returns>An enumerable used for equality comparison.</returns>
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([InterceptorType, Exception ?? null!]);
}