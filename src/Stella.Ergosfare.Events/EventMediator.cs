using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events;


/// <summary>
/// Mediates events through broadcast pipelines closed over each event's runtime type, so
/// handlers are always invoked through their typed members — including for the
/// interface-erased <see cref="PublishAsync(IEvent, EventMediationSettings?, CancellationToken)"/> overload.
/// </summary>
/// <remarks>
/// Unsealed so the DI registration can bind the engine-backed constructor through a
/// single-constructor derived shape; the facade carries no state a derived type could
/// corrupt.
/// </remarks>
/// <inheritdoc cref="IEventMediator"/>
public class EventMediator : IPublisher
{
    /// <summary>
    /// The singleton dispatch engine every publish runs against.
    /// </summary>
    private readonly MessageDispatchEngine _engine;

    /// <summary>
    /// The scope provider handlers resolve against.
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Publishes go straight to the process-wide engine's broadcast plan with
    /// <paramref name="serviceProvider"/> as the handler-resolution scope, making the
    /// facade the only object built per resolution.
    /// </summary>
    /// <param name="engine">The singleton dispatch engine.</param>
    /// <param name="serviceProvider">The provider of the scope this facade serves.</param>
    public EventMediator(
        MessageDispatchEngine engine,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _engine = engine;
        _serviceProvider = serviceProvider;
    }

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
        return _engine.BroadcastAsync(
            @event, _serviceProvider, eventMediationSettings?.Items, cancellationToken,
            eventMediationSettings?.Filters.Groups,
            eventMediationSettings?.ThrowIfNoHandlerFound ?? false);
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
        // The typed overload: when the runtime type is exactly TEvent (the overwhelmingly
        // common typed publish) the pipeline comes from a static-generic slot instead of the
        // type-keyed dictionary. The engine applies that guard itself.
        return _engine.BroadcastAsync<TEvent>(
            @event, _serviceProvider, eventMediationSettings?.Items, cancellationToken,
            eventMediationSettings?.Filters.Groups,
            eventMediationSettings?.ThrowIfNoHandlerFound ?? false);
    }

    /// <summary>
    /// Publishes an event under a canonical group filter — no settings object, and with a
    /// reused <see cref="GroupSet"/> the grouped broadcast plan matches on a single
    /// reference check. An empty set publishes the default pipeline.
    /// </summary>
    public ValueTask PublishAsync(IEvent @event, GroupSet groups, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groups);

        IEnumerable<string>? effectiveGroups = groups.Count == 0 ? null : groups;

        return _engine.BroadcastAsync(
            @event, _serviceProvider, items: null, cancellationToken, effectiveGroups);
    }

    /// <summary>
    /// Strongly-typed counterpart of
    /// <see cref="PublishAsync(IEvent, GroupSet, CancellationToken)"/>; the pipeline comes
    /// from the static-generic slot when the runtime type is exactly
    /// <typeparamref name="TEvent"/>.
    /// </summary>
    public ValueTask PublishAsync<TEvent>(TEvent @event, GroupSet groups, CancellationToken cancellationToken = default)
        where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(groups);

        IEnumerable<string>? effectiveGroups = groups.Count == 0 ? null : groups;

        return _engine.BroadcastAsync<TEvent>(
            @event, _serviceProvider, items: null, cancellationToken, effectiveGroups);
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
    public ValueTask PublishAsync(IEvent @event, ErgosfareContext context,
                             EventMediationSettings? eventMediationSettings = null)
    {
        return _engine.BroadcastAsync(
            @event, context, _serviceProvider,
            eventMediationSettings?.Filters.Groups,
            eventMediationSettings?.ThrowIfNoHandlerFound ?? false);
    }
}
