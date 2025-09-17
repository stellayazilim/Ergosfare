using System;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;

public sealed class FinishPostInterceptingSignal: PipelineSignal
{

    public static FinishPostInterceptingSignal Create(object message, object? result) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result
    };

    
    public static void Invoke(object message, object? result) => 
        SignalHubAccessor.Instance.Publish(Create(message, result));
}