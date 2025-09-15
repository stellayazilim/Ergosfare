using System;
using System.Collections.Generic;
using System.Linq;
using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.Abstractions.Events;

namespace Ergosfare.Core.Abstractions.Events;

public sealed class FinishExceptionInterceptorInvocationEvent: PipelineEvent
{
    public required Exception Exception { get; init; }
 
    public static FinishExceptionInterceptorInvocationEvent Create(object message, object? result, Exception exception) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        Exception = exception
    };
    
    
    public static void Invoke(object message, object? result, Exception exception) => 
        EventHubAccessor.Instance.Publish(Create(message, result, exception));
    
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception]);
}