using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;

/// <summary>
///     Represents a signal that is published when an exception interceptor is about to be invoked
///     in the pipeline.
/// </summary>
public sealed class BeginExceptionInterceptorInvocationSignal: PipelineSignal
{
    /// <summary>
    ///     Gets or sets the type of the interceptor being invoked.
    /// </summary>
    public required Type InterceptorType { get; init; }
    
    /// <summary>
    ///     Gets or sets the exception that was thrown during message handling.
    /// </summary>
    public required Exception Exception { get; init; }

    
    /// <summary>
    ///     Creates a new instance of <see cref="BeginExceptionInterceptorInvocationSignal"/>.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <param name="interceptorType">The type of the interceptor being invoked.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <returns>A new instance of <see cref="BeginExceptionInterceptorInvocationSignal"/>.</returns>
    public static BeginExceptionInterceptorInvocationSignal Create(object message, object? result, Type interceptorType,  Exception exception) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        InterceptorType = interceptorType,
        Exception = exception,
    };

    /// <summary>
    ///     Publishes a new <see cref="BeginExceptionInterceptorInvocationSignal"/> to the signal hub.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <param name="interceptorType">The type of the interceptor being invoked.</param>
    /// <param name="exception">The exception that occurred.</param>
    public static void Invoke(object message, object? result, Type interceptorType,  Exception exception) => 
        SignalHubAccessor.Instance.Publish(new BeginExceptionInterceptorInvocationSignal()
        {
            Message = message ?? throw new ArgumentNullException(nameof(message)),
            Result = result,
            InterceptorType = interceptorType,
            Exception = exception,
        });

    /// <summary>
    ///     Returns the components used to determine equality for this signal.
    /// </summary>
    /// <returns>An enumerable of objects that participate in equality comparison.</returns>
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception, InterceptorType]);
}