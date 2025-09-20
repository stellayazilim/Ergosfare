using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;

/// <summary>
/// Represents a signal published when a pre-interceptor finishes execution and throws an exception.
/// </summary>
public sealed class FinishPreInterceptingWithExceptionSignal: PipelineSignal
{
    /// <summary>
    /// The exception thrown by the pre-interceptor.
    /// </summary>
    public required Exception Exception { get; init; }
    
    
    /// <summary>
    /// The type of the pre-interceptor that threw the exception.
    /// </summary>
    public required Type InterceptorType { get; init; }
    
    
    /// <summary>
    /// Creates a new instance of <see cref="FinishPreInterceptingWithExceptionSignal"/> for the specified message, result, interceptor type, and exception.
    /// </summary>
    public static FinishPreInterceptingWithExceptionSignal Create(object message, object? result, Type interceptorType, Exception exception) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        InterceptorType = interceptorType,
        Exception = exception,
    };
    
    
    /// <summary>
    /// Publishes a new <see cref="FinishPreInterceptingWithExceptionSignal"/> to the signal hub.
    /// </summary>
    public static void Invoke(object message, object? result, Type interceptorType, Exception exception) => 
        SignalHubAccessor.Instance.Publish(Create(message, result, interceptorType, exception));

    /// <inheritdoc />
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception, InterceptorType]);
}