using System;

namespace Stella.Ergosfare.Core.Abstractions.SignalHub.Signals;

/// <summary>
/// Represents a signal published when a pre-interceptor finishes its invocation.
/// </summary>
public sealed class FinishPreInterceptorInvocationSignal: PipelineSignal
{
    /// <summary>
    /// Creates a new instance of <see cref="FinishPreInterceptorInvocationSignal"/> for the specified message and result.
    /// </summary>
    public static FinishPreInterceptorInvocationSignal Create(object message, object? result) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result
    };

    /// <summary>
    /// Publishes a new <see cref="FinishPreInterceptorInvocationSignal"/> to the signal hub.
    /// </summary>
    public static void Invoke(object message, object? result) => 
        SignalHubAccessor.Instance.Publish(Create(message, result));
}