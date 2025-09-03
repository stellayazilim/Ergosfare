using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.Events;

public sealed class FinishPreInterceptorInvocationEvent: PipelineEventBase
{

    public required Type InterceptorType { get; init; }

    public static FinishPreInterceptorInvocationEvent Create(Type mediatorInstance, Type messageType, Type interceptorType, Type? resultType) => new()
    {
        MediatorInstance = mediatorInstance ?? throw new ArgumentNullException(nameof(mediatorInstance)),
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType)),
        ResultType = resultType,
        InterceptorType = interceptorType ?? throw new ArgumentNullException(nameof(interceptorType)),
    };

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat(
            [InterceptorType]);
}