using System;
using System.Collections.Generic;
using System.Linq;
using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.Abstractions.Events;

namespace Ergosfare.Core.Abstractions.Events;

public sealed class BeginPreInterceptorInvocationEvent: PipelineEvent
{
    public required Type InterceptorType { get; init; }

    public static BeginPreInterceptorInvocationEvent Create(object message, object? result, Type interceptorType) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        InterceptorType = interceptorType ?? throw new ArgumentNullException(nameof(interceptorType)),
    };
    
    
    public static void Invoke(object message, object? result, Type interceptorType) => 
        EventHubAccessor.Instance.Publish(Create(message, result, interceptorType));

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat(
            [InterceptorType]);
}