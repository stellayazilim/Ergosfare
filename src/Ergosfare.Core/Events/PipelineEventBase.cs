using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.Events;

public abstract class PipelineEventBase: HubEvent
{
    public required Type MediatorInstance { get; init; }
    public required Type MessageType { get; init; }
    public required Type? ResultType { get; init; }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return MediatorInstance;
        yield return MessageType;
        yield return ResultType ?? typeof(void);
    }
}