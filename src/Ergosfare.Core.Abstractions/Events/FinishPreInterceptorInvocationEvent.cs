using System;
using System.Collections.Generic;
using System.Linq;
using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.Abstractions.Events;

public sealed class FinishPreInterceptorInvocationEvent: PipelineEvent
{


    public static FinishPreInterceptorInvocationEvent Create(object message, object? result) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result
    };

  
    public static void Invoke(object message, object? result) => 
        EventHubAccessor.Instance.Publish(Create(message, result));
}