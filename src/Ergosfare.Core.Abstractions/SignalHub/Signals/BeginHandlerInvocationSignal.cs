using System;
using System.Collections.Generic;
using System.Linq;
using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.Abstractions.SignalHub;
using Ergosfare.Core.Abstractions.SignalHub.Signals;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;
public sealed class BeginHandlerInvocationSignal: PipelineSignal
{
    public required Type HandlerType { get; init; }

    public static BeginHandlerInvocationSignal Create(object message, object?  result, Type handlerType) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType)),
    };


    public static void Invoke(object message, object? result, Type handlerType) =>
        SignalHubAccessor.Instance.Publish(new BeginHandlerInvocationSignal()
        {
            Message = message ?? throw new ArgumentNullException(nameof(message)),
            Result = result,
            HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType)),
        });

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat(
            [HandlerType]);
}