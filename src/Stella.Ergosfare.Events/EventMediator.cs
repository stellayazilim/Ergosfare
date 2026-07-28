using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
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
    /// Resolve strategy handed to the broadcast invoker; identical on both construction
    /// shapes.
    /// </summary>
    private readonly ActualTypeOrFirstAssignableTypeMessageResolveStrategy _messageResolveStrategy;

    /// <summary>
    /// Result adapters handed to the broadcast invoker; identical on both construction
    /// shapes.
    /// </summary>
    private readonly IResultAdapterService? _resultAdapterService;

    /// <summary>
    /// The mediator backing the original construction shape; null when the facade is
    /// engine-backed.
    /// </summary>
    private readonly IMessageMediator? _messageMediator;

    /// <summary>
    /// The singleton dispatch engine; null when the facade wraps an
    /// <see cref="IMessageMediator"/>.
    /// </summary>
    private readonly MessageDispatchEngine? _engine;

    /// <summary>
    /// The scope provider handlers resolve against on the engine path.
    /// </summary>
    private readonly IServiceProvider? _serviceProvider;

    /// <summary>
    /// Wraps an existing <see cref="IMessageMediator"/> — the original construction shape,
    /// kept for direct construction and foreign mediator implementations.
    /// </summary>
    public EventMediator(
        ActualTypeOrFirstAssignableTypeMessageResolveStrategy messageResolveStrategy,
        IResultAdapterService? resultAdapterService,
        IMessageMediator messageMediator)
    {
        _messageResolveStrategy = messageResolveStrategy;
        _resultAdapterService = resultAdapterService;
        _messageMediator = messageMediator;
    }

    /// <summary>
    /// Engine-backed construction: publishes go straight to the process-wide engine's
    /// broadcast plan with <paramref name="serviceProvider"/> as the handler-resolution
    /// scope, making the facade the only object built per resolution.
    /// </summary>
    /// <param name="engine">The singleton dispatch engine.</param>
    /// <param name="serviceProvider">The provider of the scope this facade serves.</param>
    /// <param name="messageResolveStrategy">Resolve strategy used to find broadcast pipelines.</param>
    /// <param name="resultAdapterService">Result adapters applied by broadcast strategies.</param>
    public EventMediator(
        MessageDispatchEngine engine,
        IServiceProvider serviceProvider,
        ActualTypeOrFirstAssignableTypeMessageResolveStrategy messageResolveStrategy,
        IResultAdapterService? resultAdapterService)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _engine = engine;
        _serviceProvider = serviceProvider;
        _messageResolveStrategy = messageResolveStrategy;
        _resultAdapterService = resultAdapterService;
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
        return _engine is not null
            ? EventBroadcastInvokerCache.Get(@event.GetType()).Publish(
                @event, eventMediationSettings, cancellationToken,
                _engine, _serviceProvider!, _messageResolveStrategy, _resultAdapterService)
            : EventBroadcastInvokerCache.Get(@event.GetType()).Publish(
                @event, eventMediationSettings, cancellationToken,
                _messageMediator!, _messageResolveStrategy, _resultAdapterService);
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
        // When the runtime type is exactly TEvent (the overwhelmingly common typed
        // publish), the static-generic holder hands back the invoker without a dictionary
        // lookup. A base-typed generic call keeps resolving by the runtime type — the
        // holder for a base TEvent would dispatch the wrong closed pipeline.
        var invoker = @event.GetType() == typeof(TEvent)
            ? EventBroadcastInvokerCache.Holder<TEvent>.Instance
            : EventBroadcastInvokerCache.Get(@event.GetType());

        return _engine is not null
            ? invoker.Publish(
                @event, eventMediationSettings, cancellationToken,
                _engine, _serviceProvider!, _messageResolveStrategy, _resultAdapterService)
            : invoker.Publish(
                @event, eventMediationSettings, cancellationToken,
                _messageMediator!, _messageResolveStrategy, _resultAdapterService);
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
        return _engine is not null
            ? EventBroadcastInvokerCache.Get(@event.GetType()).Publish(
                @event, eventMediationSettings, context.CancellationToken,
                _engine, _serviceProvider!, _messageResolveStrategy, _resultAdapterService, context)
            : EventBroadcastInvokerCache.Get(@event.GetType()).Publish(
                @event, eventMediationSettings, context.CancellationToken,
                _messageMediator!, _messageResolveStrategy, _resultAdapterService, context);
    }
}
