using System;
using System.Collections.Generic;
using System.Linq;
using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.Abstractions.SignalHub;
using Ergosfare.Core.Abstractions.SignalHub.Signals;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;
public sealed class BeginHandlingSignal: PipelineSignal
{
    // only useful for EventHandlers
    public required ushort HandlerCount { get; init; }


    public static BeginHandlingSignal Create(object message, object? result, ushort handlerCount = 0) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        HandlerCount = handlerCount,
       
    };

    
    public static void Invoke(object message, object? result, ushort handlerCount = 0) => 
        SignalHubAccessor.Instance.Publish(Create( message, result, handlerCount));

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([HandlerCount]);


}