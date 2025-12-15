using System;
using System.Collections.Generic;
using System.Linq;

namespace Stella.Ergosfare.Core.Abstractions.SignalHub.Signals;

/// <summary>
/// Represents a signal that is published at the beginning of the post-interception phase of a pipeline.
/// </summary>
public sealed class BeginPostInterceptingSignal: PipelineSignal
{
    
    /// <summary>
    /// Gets or sets the number of post-interceptors that will be executed.
    /// </summary>
    public required ushort InterceptorCount { get; init; }

    /// <summary>
    /// Creates a new instance of <see cref="BeginPostInterceptingSignal"/> with the specified message, result, and interceptor count.
    /// </summary>
    /// <param name="message">The message being processed in the pipeline.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <param name="interceptorCount">The number of post-interceptors expected to run.</param>
    /// <returns>A new <see cref="BeginPostInterceptingSignal"/> instance.</returns>
    public static BeginPostInterceptingSignal Create(object message, object? result, ushort interceptorCount = 0) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        InterceptorCount = interceptorCount,
       
    };
    
    
    /// <summary>
    /// Publishes a new <see cref="BeginPostInterceptingSignal"/> to the signal hub.
    /// </summary>
    /// <param name="message">The message being processed in the pipeline.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <param name="interceptorCount">The number of post-interceptors expected to run.</param>
    public static void Invoke(object message, object? result, ushort interceptorCount = 0) => 
        SignalHubAccessor.Instance.Publish(Create(message, result, interceptorCount));

    
    /// <summary>
    /// Returns the components used to determine equality for this signal.
    /// </summary>
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([InterceptorCount]);
}   