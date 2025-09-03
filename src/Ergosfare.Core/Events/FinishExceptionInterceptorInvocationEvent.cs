namespace Ergosfare.Core.Events;

public sealed class FinishExceptionInterceptorInvocationEvent: PipelineEvent
{
    public required Exception Exception { get; init; }
 
    public static FinishExceptionInterceptorInvocationEvent Create(Type mediatorInstance, Type messageType, Type? resultType, Exception exception) => new()
    {
        Exception = exception,
        MediatorInstance = mediatorInstance ?? throw new ArgumentNullException(nameof(mediatorInstance)),
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType)),
        ResultType = resultType,
    };

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception]);
}