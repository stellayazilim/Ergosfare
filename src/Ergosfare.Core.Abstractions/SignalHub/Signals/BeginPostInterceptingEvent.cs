using System;
using System.Collections.Generic;
using System.Linq;
using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.Abstractions.SignalHub;
using Ergosfare.Core.Abstractions.SignalHub.Signals;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;
public sealed class BeginPostInterceptingSignal: PipelineSignal
{
    public required ushort InterceptorCount { get; init; }


    public static BeginPostInterceptingSignal Create(object message, object? result, ushort interceptorCount = 0) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        InterceptorCount = interceptorCount,
       
    };
    
    
    public static void Invoke(object message, object? result, ushort interceptorCount = 0) => 
        SignalHubAccessor.Instance.Publish(Create(message, result, interceptorCount));

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([InterceptorCount]);
}   