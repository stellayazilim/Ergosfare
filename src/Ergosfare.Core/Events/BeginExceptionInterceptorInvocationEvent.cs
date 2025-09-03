namespace Ergosfare.Core.Events.ExceptionIntercept;

public sealed class BeginExceptionInterceptorInvocationEvent: PipelineEventBase
{
    public required Exception Exception { get; init; }

    public static BeginExceptionInterceptorInvocationEvent Create(Type mediatorInstance, Type messageType, Type? resultType, Exception exception) => new()
    {
        Exception = exception,
        MediatorInstance = mediatorInstance ?? throw new ArgumentNullException(nameof(mediatorInstance)),
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType)),
        ResultType = resultType,
    };

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception]);
}