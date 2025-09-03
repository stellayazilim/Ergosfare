namespace Ergosfare.Core.Events;

public sealed class FinishPostInterceptingEvent: PipelineEvent
{

    public static BeginPostInterceptingEvent Create(Type mediatorInstance, Type messageType, Type? resultType, ushort interceptorCount = 0) => new()
    {
        InterceptorCount = interceptorCount,
        MediatorInstance = mediatorInstance ?? throw new ArgumentNullException(nameof(mediatorInstance)),
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType)),
        ResultType = resultType,
    };

}