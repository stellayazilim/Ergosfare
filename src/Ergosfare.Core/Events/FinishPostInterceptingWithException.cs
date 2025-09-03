namespace Ergosfare.Core.Events;

public sealed class FinishPostInterceptingWithException: PipelineEventBase
{
    public required Exception Exception { get; init; }
    public static FinishPostInterceptingWithException Create(Type mediatorInstance, Type messageType, Exception exception, Type? resultType) => new()
    {
        Exception = exception,
        MediatorInstance = mediatorInstance ?? throw new ArgumentNullException(nameof(mediatorInstance)),
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType)),
        ResultType = resultType
    };

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception]);
}