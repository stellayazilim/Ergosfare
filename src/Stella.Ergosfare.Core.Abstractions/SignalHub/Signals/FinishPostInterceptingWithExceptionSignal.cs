using System;
using System.Collections.Generic;
using System.Linq;

namespace Stella.Ergosfare.Core.Abstractions.SignalHub.Signals;

/// <summary>
/// Represents a signal published after a post-interceptor has thrown an exception during message processing.
/// </summary>
public sealed class FinishPostInterceptingWithExceptionSignal: PipelineSignal
{
    /// <summary>
    /// The type of the interceptor that caused the exception.
    /// </summary>
    public required Type InterceptorType { get; init; }
    
    /// <summary>
    /// The exception thrown by the interceptor.
    /// </summary>
    public required Exception Exception { get; init; }
    
    /// <summary>
    /// Creates a new instance of <see cref="FinishPostInterceptingWithExceptionSignal"/> with the specified parameters.
    /// </summary>
    /// <param name="message">The message that was being processed.</param>
    /// <param name="result">The result of the message processing, if any.</param>
    /// <param name="interceptorType">The type of the interceptor that threw the exception.</param>
    /// <param name="exception">The exception thrown by the interceptor.</param>
    /// <returns>A new <see cref="FinishPostInterceptingWithExceptionSignal"/> instance.</returns>
    public static FinishPostInterceptingWithExceptionSignal Create(object message, object? result, Type interceptorType, Exception exception) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        InterceptorType = interceptorType,
        Exception = exception,
    };

    
    /// <summary>
    /// Publishes a new <see cref="FinishPostInterceptingWithExceptionSignal"/> to the signal hub.
    /// </summary>
    /// <param name="message">The message that was being processed.</param>
    /// <param name="result">The result of the message processing, if any.</param>
    /// <param name="interceptorType">The type of the interceptor that threw the exception.</param>
    /// <param name="exception">The exception thrown by the interceptor.</param>
    public static void Invoke(object message, object? result, Type interceptorType, Exception exception) => 
        SignalHubAccessor.Instance.Publish(Create(message, result, interceptorType, exception));
    
    
    /// <inheritdoc />
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception, InterceptorType]);
}