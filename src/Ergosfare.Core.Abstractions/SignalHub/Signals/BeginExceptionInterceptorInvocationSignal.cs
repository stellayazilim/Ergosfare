using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;
public sealed class BeginExceptionInterceptorInvocationSignal: PipelineSignal
{
    public required Type InterceptorType { get; init; }
    public required Exception Exception { get; init; }

    public static BeginExceptionInterceptorInvocationSignal Create(object message, object? result, Type interceptorType,  Exception exception) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        InterceptorType = interceptorType,
        Exception = exception,
    };

    public static void Invoke(object message, object? result, Type interceptorType,  Exception exception) => 
        SignalHubAccessor.Instance.Publish(new BeginExceptionInterceptorInvocationSignal()
        {
            Message = message ?? throw new ArgumentNullException(nameof(message)),
            Result = result,
            InterceptorType = interceptorType,
            Exception = exception,
        });

    
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception, InterceptorType]);
}