using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.Abstractions.SignalHub;
using Ergosfare.Core.Abstractions.SignalHub.Signals;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;
public sealed class BeginPostInterceptorInvocationSignal: PipelineSignal
{
    
    public required Type InterceptorType { get; init; }

    public static BeginPostInterceptorInvocationSignal Create(object message, object? result, Type interceptorType) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        InterceptorType = interceptorType ?? throw new ArgumentNullException(nameof(interceptorType)),
    };

    public static void Invoke(object message, object? result, Type interceptorType) => 
        SignalHubAccessor.Instance.Publish(Create(message, result, interceptorType));
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat(
            [InterceptorType]);
}