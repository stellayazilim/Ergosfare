namespace Ergosfare.Core.Events.ExceptionIntercept;

public class FinishExceptionInterceptingEvent: PipelineEventBase
{
    public required Exception Exception { get; init; }
    public required Exception FinalException { get; init; }
    
    public static FinishExceptionInterceptingEvent Create(Type mediatorInstance, Type messageType, Type? resultType, Exception exception, Exception finalException) => new()
    {
        Exception = exception,
        FinalException = finalException,
        MediatorInstance = mediatorInstance ?? throw new ArgumentNullException(nameof(mediatorInstance)),
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType)),
        ResultType = resultType,
    };

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception, FinalException]);
}