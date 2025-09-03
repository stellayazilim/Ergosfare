namespace Ergosfare.Core.Events;

public sealed class FinishPreInterceptingEvent: PipelineEventBase
{
    public static FinishPipelineEvent Create(Type mediatorInstance, Type messageType, Type? resultType) => new()
    {
        MediatorInstance = mediatorInstance ?? throw new ArgumentNullException(nameof(mediatorInstance)),
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType)),
        ResultType = resultType,
    }; 

}