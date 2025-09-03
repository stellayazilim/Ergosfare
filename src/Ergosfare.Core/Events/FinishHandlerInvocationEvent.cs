namespace Ergosfare.Core.Events;

public sealed class FinishHandlerInvocationEvent: PipelineEvent
{
    public required Type HandlerType { get; init; }

    public static FinishHandlerInvocationEvent Create(Type mediatorInstance, Type messageType, Type handlerType, Type? resultType) => new()
    {
        MediatorInstance = mediatorInstance ?? throw new ArgumentNullException(nameof(mediatorInstance)),
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType)),
        ResultType = resultType,
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType)),
    };

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat(
            [HandlerType]);
}