using Ergosfare.Contracts;

namespace Ergosfare.Events.Abstractions.Extensions;

public static class EventMediatorExtensions
{

    public static Task PublishAsync(this IEventMediator eventMediator, IEvent @event, CancellationToken cancellationToken = default)
    {
        return eventMediator.PublishAsync(@event, null, cancellationToken);
    }

    public static Task PublishAsync(this IEventMediator eventMediator, IEvent @event, string[] tag, CancellationToken cancellationToken = default)
    {
        return eventMediator.PublishAsync(@event,
            new EventMediationSettings
            {
                Filters =
                {
                    Tags = tag
                }
            },
            cancellationToken);
    }
}