using System;
using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.Abstractions.Events;

public sealed class FinishPreInterceptingEvent: PipelineEvent
{
    public static FinishPreInterceptingEvent Create(object message, object? result) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result
    }; 

    
    public static void Invoke(object message, object? result) => 
        EventHubAccessor.Instance.Publish(Create(message, result));
}