using System;
using System.Collections.Generic;
using System.Linq;
using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.Abstractions.Events;

namespace Ergosfare.Core.Abstractions.Events;

public sealed class BeginPostInterceptingEvent: PipelineEvent
{
    public required ushort InterceptorCount { get; init; }


    public static BeginPostInterceptingEvent Create(object message, object? result, ushort interceptorCount = 0) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        InterceptorCount = interceptorCount,
       
    };
    
    
    public static void Invoke(object message, object? result, ushort interceptorCount = 0) => 
        EventHubAccessor.Instance.Publish(Create(message, result, interceptorCount));

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([InterceptorCount]);
}   