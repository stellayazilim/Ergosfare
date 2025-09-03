using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.EventHub;

namespace Ergosfare.Core.Events;

public abstract class PipelineEvent: HubEvent
{
    public required Type MediatorInstance { get; init; }
    public required Type MessageType { get; init; }
    public required Type? ResultType { get; init; }

    public static IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : PipelineEvent
    {
        return EventHubAccessor.Instance.Subscribe(handler);
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return MediatorInstance;
        yield return MessageType;
        yield return ResultType ?? typeof(void);
    }
}