namespace Ergosfare.Core.Events;

public sealed class BeginPipelineEvent : PipelineEvent
{
 
    public static BeginPipelineEvent Create(Type mediatorInstance, Type messageType, Type? resultType) => new()
    {
        MediatorInstance = mediatorInstance ?? throw new ArgumentNullException(nameof(mediatorInstance)),
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType)),
        ResultType = resultType,
    }; 
        
}