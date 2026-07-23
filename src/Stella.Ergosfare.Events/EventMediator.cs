using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events;


/// <summary>
/// Mediates events through broadcast pipelines closed over each event's runtime type, so
/// handlers are always invoked through their typed members — including for the
/// interface-erased <see cref="PublishAsync(IEvent, EventMediationSettings?, CancellationToken)"/> overload.
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
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous publish operation.</returns>
    public ValueTask PublishAsync(IEvent @event,
                             EventMediationSettings? eventMediationSettings = null,
                             CancellationToken cancellationToken = default)
    {
        return EventBroadcastInvokerCache.Get(@event.GetType()).Publish(
            @event, eventMediationSettings ?? new EventMediationSettings(), cancellationToken,
            messageMediator, messageResolveStrategy, resultAdapterService);
    }

    /// <summary>
    /// Publishes a strongly-typed event asynchronously through the mediation pipeline.
    /// </summary>
    /// <typeparam name="TEvent">The event type being published.</typeparam>
    /// <param name="event">The event message to publish.</param>
    /// <param name="eventMediationSettings">Optional settings for pipeline execution.</param>
    /// <param name="cancellationToken">Cancellation token for async execution.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous publish operation.</returns>
    public ValueTask PublishAsync<TEvent>(TEvent @event,
                                     EventMediationSettings? eventMediationSettings = null,
                                     CancellationToken cancellationToken = default) where TEvent : notnull
    {
        return EventBroadcastInvokerCache.Get(@event.GetType()).Publish(
            @event, eventMediationSettings ?? new EventMediationSettings(), cancellationToken,
            messageMediator, messageResolveStrategy, resultAdapterService);
    }

    /// <summary>
    /// Publishes an event under an externally owned execution context — the
    /// nested-dispatch path: a handler opens a scope on its own context and passes the
    /// child here. The caller owns the context's lifetime; cancellation flows from the
    /// context.
    /// </summary>
    /// <param name="event">The event message to publish.</param>
    /// <param name="context">The externally owned execution context to publish under.</param>
    /// <param name="eventMediationSettings">Optional settings for pipeline execution.</param>
    public ValueTask PublishAsync(IEvent @event, IExecutionContext context,
                             EventMediationSettings? eventMediationSettings = null)
    {
        return EventBroadcastInvokerCache.Get(@event.GetType()).Publish(
            @event, eventMediationSettings ?? new EventMediationSettings(), context.CancellationToken,
            messageMediator, messageResolveStrategy, resultAdapterService, context);
    }
}
