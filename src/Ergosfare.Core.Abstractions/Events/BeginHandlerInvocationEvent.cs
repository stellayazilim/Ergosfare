using System;
using System.Collections.Generic;
using System.Linq;
using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.Abstractions.Events;

namespace Ergosfare.Core.Abstractions.Events;

public sealed class BeginHandlerInvocationEvent: PipelineEvent
{
    public required Type HandlerType { get; init; }

    public static BeginHandlerInvocationEvent Create(object message, object?  result, Type handlerType) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType)),
    };


    public static void Invoke(object message, object? result, Type handlerType) =>
        EventHubAccessor.Instance.Publish(new BeginHandlerInvocationEvent()
        {
            Message = message ?? throw new ArgumentNullException(nameof(message)),
            Result = result,
            HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType)),
        });

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat(
            [HandlerType]);
}