using System;
using System.Collections.Generic;
using System.Linq;
using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.Abstractions.Events;

/// <summary>
/// Event that signals the start of an individual final interceptor invocation in the pipeline.
/// Raised before a specific final interceptor runs, allowing subscribers to track or log
/// the processing of each final interceptor for a message.
/// </summary>
public class BeginFinalInterceptorInvocationEvent: PipelineEvent
{
    /// <summary>
    /// The <see cref="Type"/> of the final interceptor that is about to be invoked.
    /// </summary>
    public required Type HandlerType { get; init; }
    
    /// <summary>
    /// Optional exception that may have occurred prior to this final interceptor invocation.
    /// </summary>
    public Exception? Exception { get; init; }
    
    
    /// <summary>
    /// Creates a new instance of <see cref="BeginFinalInterceptorInvocationEvent"/>.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The current result of the message, if any.</param>
    /// <param name="exception">An optional exception captured earlier in the pipeline.</param>
    /// <param name="handlerType">The type of the final interceptor that is about to execute.</param>
    /// <returns>A new <see cref="BeginFinalInterceptorInvocationEvent"/> instance.</returns>
    public static BeginFinalInterceptorInvocationEvent Create(object message, object? result, Exception? exception, Type handlerType) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType)),
        Exception = exception
    };
    
    
    /// <summary>
    /// Publishes this event to the <see cref="EventHubAccessor"/> for subscribers to track
    /// the start of a final interceptor invocation.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The current result of the message, if any.</param>
    /// <param name="exception">An optional exception captured earlier in the pipeline.</param>
    /// <param name="handlerType">The type of the final interceptor that is about to execute.</param>
    public static void Invoke(object message, object? result, Exception? exception, Type handlerType) =>
        EventHubAccessor.Instance.Publish(Create(message, result, exception, handlerType));
    
    
    /// <summary>
    /// Returns components that define equality for this event instance.
    /// Includes the base equality components, the handler type, and any attached exception.
    /// </summary>
    /// <returns>An enumerable used for equality comparison.</returns>
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([HandlerType, Exception ?? null!]);
}