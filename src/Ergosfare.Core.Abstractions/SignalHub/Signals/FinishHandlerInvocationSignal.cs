using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;

public sealed class FinishHandlerInvocationSignal: PipelineSignal
{
    public required Type HandlerType { get; init; }

    public static FinishHandlerInvocationSignal Create(object message, object?  result, Type? handlerType) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType)),
    };
    
    
    
    public static void Invoke(object message, object?  result, Type? handlerType) => SignalHubAccessor.Instance.Publish(
        new FinishHandlerInvocationSignal()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType)),
    });

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat(
            [HandlerType]);
}