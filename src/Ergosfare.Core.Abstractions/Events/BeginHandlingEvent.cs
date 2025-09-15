using System;
using System.Collections.Generic;
using System.Linq;
using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.Abstractions.Events;

namespace Ergosfare.Core.Abstractions.Events;

public sealed class BeginHandlingEvent: PipelineEvent
{
    // only useful for EventHandlers
    public required ushort HandlerCount { get; init; }


    public static BeginHandlingEvent Create(object message, object? result, ushort handlerCount = 0) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        HandlerCount = handlerCount,
       
    };

    
    public static void Invoke(object message, object? result, ushort handlerCount = 0) => 
        EventHubAccessor.Instance.Publish(Create( message, result, handlerCount));

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([HandlerCount]);


}