using System;
using System.Collections.Generic;
using System.Linq;
using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.Abstractions.Events;

public sealed class BeginExceptionInterceptorInvocationEvent: PipelineEvent
{
    public required Type HandlerType { get; init; }
    public required Exception Exception { get; init; }

    public static BeginExceptionInterceptorInvocationEvent Create(object message, object? result, Type handlerType,  Exception exception) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        HandlerType = handlerType,
        Exception = exception,
    };

    public static void Invoke(object message, object? result, Type handlerType,  Exception exception) => 
        EventHubAccessor.Instance.Publish(new BeginExceptionInterceptorInvocationEvent()
        {
            Message = message ?? throw new ArgumentNullException(nameof(message)),
            Result = result,
            HandlerType = handlerType,
            Exception = exception,
        });

    
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception, HandlerType]);
}