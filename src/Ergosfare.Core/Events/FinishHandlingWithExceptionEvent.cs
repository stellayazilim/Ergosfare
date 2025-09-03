namespace Ergosfare.Core.Events;

public sealed class FinishHandlingWithExceptionEvent: PipelineEvent
{
    public required Exception Exception { get; init; }
    public static FinishHandlingWithExceptionEvent Create(Type mediatorInstance, Type messageType, Exception exception, Type? resultType) => new()
    {
        Exception = exception,
        MediatorInstance = mediatorInstance ?? throw new ArgumentNullException(nameof(mediatorInstance)),
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType)),
        ResultType = resultType
    };

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception]);

}