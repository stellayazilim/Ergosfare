namespace Ergosfare.Core.Events;

public sealed class BeginHandlingEvent: PipelineEvent
{
    // only useful for EventHandlers
    public required ushort HandlerCount { get; init; }


    public static BeginHandlingEvent Create(Type mediatorInstance, Type messageType, Type? resultType, ushort handlerCount = 0) => new()
    {
        HandlerCount = handlerCount,
        MediatorInstance = mediatorInstance ?? throw new ArgumentNullException(nameof(mediatorInstance)),
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType)),
        ResultType = resultType
    };

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([HandlerCount]);


}