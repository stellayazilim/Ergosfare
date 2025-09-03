namespace Ergosfare.Core.Events;

public sealed class FinishPostInterceptingWithExceptionEvent: PipelineEvent
{
    public required Type InterceptorType { get; init; }
    public required Exception Exception { get; init; }
    public static FinishPostInterceptingWithExceptionEvent Create(Type mediatorInstance, Type messageType, Type interceptorType, Exception exception, Type? resultType) => new()
    {
        InterceptorType = interceptorType,
        Exception = exception,
        MediatorInstance = mediatorInstance ?? throw new ArgumentNullException(nameof(mediatorInstance)),
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType)),
        ResultType = resultType
    };

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception, InterceptorType]);
}