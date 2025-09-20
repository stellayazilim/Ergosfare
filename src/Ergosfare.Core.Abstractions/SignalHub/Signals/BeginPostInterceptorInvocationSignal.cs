using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;

/// <summary>
/// Represents a signal that is published at the beginning of the post-interceptor invocation phase of a pipeline.
/// </summary>
public sealed class BeginPostInterceptorInvocationSignal: PipelineSignal
{
    /// <summary>
    /// Gets or sets the type of the interceptor being invoked.
    /// </summary>
    public required Type InterceptorType { get; init; }

    
    /// <summary>
    /// Creates a new instance of <see cref="BeginPostInterceptorInvocationSignal"/> with the specified message, result, and interceptor type.
    /// </summary>
    /// <param name="message">The message being processed in the pipeline.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <param name="interceptorType">The type of the post-interceptor being invoked.</param>
    /// <returns>A new <see cref="BeginPostInterceptorInvocationSignal"/> instance.</returns>
    public static BeginPostInterceptorInvocationSignal Create(object message, object? result, Type interceptorType) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        InterceptorType = interceptorType ?? throw new ArgumentNullException(nameof(interceptorType)),
    };

    
    /// <summary>
    /// Publishes a new <see cref="BeginPostInterceptorInvocationSignal"/> to the signal hub.
    /// </summary>
    /// <param name="message">The message being processed in the pipeline.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <param name="interceptorType">The type of the post-interceptor being invoked.</param>
    public static void Invoke(object message, object? result, Type interceptorType) => 
        SignalHubAccessor.Instance.Publish(Create(message, result, interceptorType));
    
    
    /// <summary>
    /// Returns the components used to determine equality for this signal.
    /// </summary>
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat(
            [InterceptorType]);
}