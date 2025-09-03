namespace Ergosfare.Core.Events;

public sealed class BeginExceptionInterceptingEvent: PipelineEvent
{
    
    public required ushort InterceptorCount { get; init; }
    public required Exception Exception { get; init; }

    public static BeginExceptionInterceptingEvent Create(Type mediatorInstance, Type messageType, Type? resultType, Exception exception, ushort interceptorCount = 0) => new()
    {
        Exception = exception,
        InterceptorCount = interceptorCount,
        MediatorInstance = mediatorInstance ?? throw new ArgumentNullException(nameof(mediatorInstance)),
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType)),
        ResultType = resultType,
    };

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([InterceptorCount, Exception]);
}