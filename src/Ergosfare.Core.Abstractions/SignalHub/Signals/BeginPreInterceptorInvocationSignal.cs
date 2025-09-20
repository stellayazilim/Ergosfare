using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;

/// <summary>
/// Represents a signal that is published at the beginning of the invocation of a pre-interceptor in the pipeline.
/// </summary>
public sealed class BeginPreInterceptorInvocationSignal: PipelineSignal
{
    /// <summary>
    /// Gets or sets the type of the pre-interceptor being invoked.
    /// </summary>
    public required Type InterceptorType { get; init; }

    
    /// <summary>
    /// Creates a new instance of <see cref="BeginPreInterceptorInvocationSignal"/> with the specified message, result, and interceptor type.
    /// </summary>
    /// <param name="message">The message being processed in the pipeline.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <param name="interceptorType">The type of the pre-interceptor being invoked.</param>
    /// <returns>A new <see cref="BeginPreInterceptorInvocationSignal"/> instance.</returns>
    public static BeginPreInterceptorInvocationSignal Create(object message, object? result, Type interceptorType) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        InterceptorType = interceptorType ?? throw new ArgumentNullException(nameof(interceptorType)),
    };
    
    
    /// <summary>
    /// Publishes a new <see cref="BeginPreInterceptorInvocationSignal"/> to the signal hub.
    /// </summary>
    /// <param name="message">The message being processed in the pipeline.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <param name="interceptorType">The type of the pre-interceptor being invoked.</param>
    public static void Invoke(object message, object? result, Type interceptorType) => 
        SignalHubAccessor.Instance.Publish(Create(message, result, interceptorType));

    
    /// <summary>
    /// Returns the components used to determine equality for this signal.
    /// </summary>
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat(
            [InterceptorType]);
}