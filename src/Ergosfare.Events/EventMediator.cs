using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.Abstractions.SignalHub.Signals;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Events.Abstractions;

namespace Ergosfare.Events;


/// <summary>
/// Mediates events through the framework message pipeline, supporting both generic and non-generic events.
/// Responsible for publishing events to all registered handlers asynchronously, optionally applying
/// result adapters and event mediation settings.
/// </summary>
/// <inheritdoc cref="IEventMediator"/>
public sealed class EventMediator(
    ActualTypeOrFirstAssignableTypeMessageResolveStrategy messageResolveStrategy,
    IResultAdapterService? resultAdapterService,
    IMessageMediator messageMediator) : IPublisher
{
    
    /// <summary>
    /// Publishes a non-generic event asynchronously through the mediation pipeline.
    /// </summary>
    /// <param name="event">The event message to publish.</param>
    /// <param name="eventMediationSettings">Optional settings for pipeline execution, e.g., filters, items, and exception behavior.</param>
    /// <param name="cancellationToken">Cancellation token for async execution.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous publish operation.</returns>
    public async Task PublishAsync(IEvent @event,
                             EventMediationSettings? eventMediationSettings = null,
                             CancellationToken cancellationToken = default)
    {
        // Trigger the pipeline start event
        BeginPipelineSignal.Invoke(@event, null);
        // Create a broadcast strategy for sending this event to multiple handlers
        var mediationStrategy = new AsyncBroadcastMediationStrategy<IEvent>(resultAdapterService, eventMediationSettings ??= new EventMediationSettings());

        // Execute the event through the message mediator
        await  messageMediator.Mediate(@event,
            new MediateOptions<IEvent, Task>
            {
                MessageMediationStrategy = mediationStrategy,
                MessageResolveStrategy = messageResolveStrategy,
                CancellationToken = cancellationToken,
                RegisterPlainMessagesOnSpot = !eventMediationSettings.ThrowIfNoHandlerFound,
                Items = eventMediationSettings.Items,
                Groups = eventMediationSettings.Filters.Groups
            });
        // Trigger the pipeline finish event
        FinishPipelineSignal.Invoke(@event, null);
    }

    
    /// <summary>
    /// Publishes a strongly-typed poco generic event asynchronously through the mediation pipeline.
    /// </summary>
    /// <typeparam name="TEvent">The concrete event type.</typeparam>
    /// <param name="event">The strongly-typed poco event message to publish.</param>
    /// <param name="eventMediationSettings">Optional settings for pipeline execution, e.g., filters, items, and exception behavior.</param>
    /// <param name="cancellationToken">Cancellation token for async execution.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous publish operation.</returns>
    public async Task PublishAsync<TEvent>(TEvent @event,
                                     EventMediationSettings? eventMediationSettings = null,
                                     CancellationToken cancellationToken = default) where TEvent : notnull
    {
        // Trigger the pipeline start event
        BeginPipelineSignal.Invoke(@event, null);
        // Create a broadcast strategy for this specific event type
        var mediationStrategy = new AsyncBroadcastMediationStrategy<TEvent>(resultAdapterService, eventMediationSettings ??= new EventMediationSettings());
        // Execute the strongly-typed event through the mediator
        await messageMediator.Mediate(@event,
            new MediateOptions<TEvent, Task>
            {
                MessageMediationStrategy = mediationStrategy,
                MessageResolveStrategy = messageResolveStrategy,
                CancellationToken = cancellationToken,
                RegisterPlainMessagesOnSpot = !eventMediationSettings.ThrowIfNoHandlerFound,
                Items = eventMediationSettings.Items,
                Groups = eventMediationSettings.Filters.Groups
            });
        // Trigger the pipeline finish event
        FinishPipelineSignal.Invoke(@event, null);
    }
}