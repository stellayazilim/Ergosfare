using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.Events;

public sealed class BeginPreInterceptingEvent: PipelineEvent
{
    public required ushort InterceptorCount { get; init; }


    public static BeginPreInterceptingEvent Create(Type mediatorInstance, Type messageType, Type? resultType, ushort interceptorCount = 0) => new()
    {
        InterceptorCount = interceptorCount,
        MediatorInstance = mediatorInstance ?? throw new ArgumentNullException(nameof(mediatorInstance)),
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType)),
        ResultType = resultType,
    };

    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([InterceptorCount]);

  
}