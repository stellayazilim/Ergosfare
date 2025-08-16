using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Events.Abstractions;

namespace Ergosfare.Events;


/// <inheritdoc cref="IEventMediator" />
public sealed class EventMediator : IPublisher
{
    private readonly IMessageMediator _messageMediator;

    public EventMediator(IMessageMediator messageMediator)
    {
        _messageMediator = messageMediator;
    }

    public Task PublishAsync(IEvent @event,
                             EventMediationSettings? eventMediationSettings = null,
                             CancellationToken cancellationToken = default)
    {
        eventMediationSettings ??= new EventMediationSettings();
        var mediationStrategy = new AsyncBroadcastMediationStrategy<IEvent>(eventMediationSettings);
        var resolveStrategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();

        return _messageMediator.Mediate(@event,
            new MediateOptions<IEvent, Task>
            {
                MessageMediationStrategy = mediationStrategy,
                MessageResolveStrategy = resolveStrategy,
                CancellationToken = cancellationToken,
                RegisterPlainMessagesOnSpot = !eventMediationSettings.ThrowIfNoHandlerFound,
                Items = eventMediationSettings.Items
            });
    }

    public Task PublishAsync<TEvent>(TEvent @event,
                                     EventMediationSettings? eventMediationSettings = null,
                                     CancellationToken cancellationToken = default) where TEvent : notnull
    {
        eventMediationSettings ??= new EventMediationSettings();
        var mediationStrategy = new AsyncBroadcastMediationStrategy<TEvent>(eventMediationSettings);
        var resolveStrategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();

        return _messageMediator.Mediate(@event,
            new MediateOptions<TEvent, Task>
            {
                MessageMediationStrategy = mediationStrategy,
                MessageResolveStrategy = resolveStrategy,
                CancellationToken = cancellationToken,
                RegisterPlainMessagesOnSpot = !eventMediationSettings.ThrowIfNoHandlerFound,
                Items = eventMediationSettings.Items
            });
    }
}