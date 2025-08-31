using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Events.Abstractions;

namespace Ergosfare.Events;


/// <inheritdoc cref="IEventMediator" />
public sealed class EventMediator(IMessageMediator messageMediator) : IPublisher
{
    public Task PublishAsync(IEvent @event,
                             EventMediationSettings? eventMediationSettings = null,
                             CancellationToken cancellationToken = default)
    {
        var mediationStrategy = new AsyncBroadcastMediationStrategy<IEvent>(eventMediationSettings ??= new EventMediationSettings());
        var resolveStrategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();

        return messageMediator.Mediate(@event,
            new MediateOptions<IEvent, Task>
            {
                MessageMediationStrategy = mediationStrategy,
                MessageResolveStrategy = resolveStrategy,
                CancellationToken = cancellationToken,
                RegisterPlainMessagesOnSpot = !eventMediationSettings.ThrowIfNoHandlerFound,
                Items = eventMediationSettings.Items,
                Groups = eventMediationSettings.Filters.Groups
            });
    }

    public Task PublishAsync<TEvent>(TEvent @event,
                                     EventMediationSettings? eventMediationSettings = null,
                                     CancellationToken cancellationToken = default) where TEvent : notnull
    {
        var mediationStrategy = new AsyncBroadcastMediationStrategy<TEvent>(eventMediationSettings ??= new EventMediationSettings());
        var resolveStrategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();

        return messageMediator.Mediate(@event,
            new MediateOptions<TEvent, Task>
            {
                MessageMediationStrategy = mediationStrategy,
                MessageResolveStrategy = resolveStrategy,
                CancellationToken = cancellationToken,
                RegisterPlainMessagesOnSpot = !eventMediationSettings.ThrowIfNoHandlerFound,
                Items = eventMediationSettings.Items,
                Groups = eventMediationSettings.Filters.Groups
            });
    }
}