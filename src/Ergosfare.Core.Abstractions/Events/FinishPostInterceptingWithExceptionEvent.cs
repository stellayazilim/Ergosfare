using System;
using System.Collections.Generic;
using System.Linq;
using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.Abstractions.Events;

public sealed class FinishPostInterceptingWithExceptionEvent: PipelineEvent
{
    public required Type InterceptorType { get; init; }
    public required Exception Exception { get; init; }
    public static FinishPostInterceptingWithExceptionEvent Create(object message, object? result, Type interceptorType, Exception exception) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        InterceptorType = interceptorType,
        Exception = exception,
    };

    public static void Invoke(object message, object? result, Type interceptorType, Exception exception) => 
        EventHubAccessor.Instance.Publish(Create(message, result, interceptorType, exception));
    
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception, InterceptorType]);
}