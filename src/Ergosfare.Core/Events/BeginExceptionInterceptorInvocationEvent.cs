namespace Ergosfare.Core.Events;

public sealed class BeginExceptionInterceptorInvocationEvent: PipelineEvent
{
    public required Type HandlerType { get; init; }
    public required Exception Exception { get; init; }

    public static BeginExceptionInterceptorInvocationEvent Create(Type mediatorInstance, Type messageType, Type handlerType, Type? resultType, Exception exception) => new()
    {
        HandlerType = handlerType,
        Exception = exception,
        MediatorInstance = mediatorInstance ?? throw new ArgumentNullException(nameof(mediatorInstance)),
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType)),
        ResultType = resultType,
    };

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception, HandlerType]);
}