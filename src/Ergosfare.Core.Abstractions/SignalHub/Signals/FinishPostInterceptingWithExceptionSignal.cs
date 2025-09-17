using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;

public sealed class FinishPostInterceptingWithExceptionSignal: PipelineSignal
{
    public required Type InterceptorType { get; init; }
    public required Exception Exception { get; init; }
    public static FinishPostInterceptingWithExceptionSignal Create(object message, object? result, Type interceptorType, Exception exception) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        InterceptorType = interceptorType,
        Exception = exception,
    };

    public static void Invoke(object message, object? result, Type interceptorType, Exception exception) => 
        SignalHubAccessor.Instance.Publish(Create(message, result, interceptorType, exception));
    
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception, InterceptorType]);
}