using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Internal.Mediator;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events;


/// <summary>
/// The event mediator an application resolves: it holds the scope it was resolved from and
/// the container's publish pipelines.
/// </summary>
/// <remarks>
/// <para>
/// Every publish method is one body — check the argument, find the pipeline, run it. They
/// deliberately do not call each other: each entry a caller can land on carries the whole
/// path, because a relay frame between the facade and the pipeline costs more than it saves.
/// </para>
/// <para>
/// The class is not sealed so that registration can bind its engine-backed constructor
/// through a derived shape; it holds no state a derived type could disturb.
/// </para>
/// </remarks>
public class EventMediator : IPublisher
{
    /// <summary>
    /// The container's publish pipelines, taken from the engine once so a publish reads a
    /// field rather than going through the engine.
    /// </summary>
    private readonly FrozenBroadcastTable _broadcasts;

    /// <summary>
    /// The provider of the scope this facade was resolved from; handlers resolve against it.
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes the facade over a container's engine and the scope it serves.
    /// </summary>
    /// <param name="engine">The container's dispatch engine.</param>
    /// <param name="serviceProvider">The provider of the scope this facade serves.</param>
    /// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
    public EventMediator(
        MessageDispatchEngine engine,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _broadcasts = engine.Broadcasts;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public ValueTask PublishAsync(IEvent @event, IEnumerable<string>? groups,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return _broadcasts.Get(@event.GetType())
            .PublishPooled(@event, _serviceProvider, cancellationToken, groups);
    }

    /// <inheritdoc />
    public ValueTask PublishAsync<TEvent>(TEvent @event, IEnumerable<string>? groups,
        CancellationToken cancellationToken)
        where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(@event);

        // With the runtime type exactly TEvent — the common typed publish — the pipeline
        // comes from a static generic slot instead of the type-keyed table. Publishing
        // through a base type keeps resolving by the runtime type, since the slot for the
        // base type holds the wrong pipeline.
        var dispatch = @event.GetType() == typeof(TEvent)
            ? _broadcasts.Get<TEvent>()
            : _broadcasts.Get(@event.GetType());

        return dispatch.PublishPooled(@event, _serviceProvider, cancellationToken, groups);
    }

    /// <inheritdoc />
    public ValueTask PublishAsync(IEvent @event, ErgosfareContext context, IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);

        return _broadcasts.Get(@event.GetType())
            .Publish(@event, context, _serviceProvider, groups);
    }

    /// <summary>
    /// Publishes <paramref name="event"/> through its default pipeline.
    /// </summary>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="event"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The conveniences are declared on this class as well as on the interface. A call made
    /// through the concrete type does not find a default interface method, so declaring them
    /// only on the interface would leave those calls without an overload to bind to. Each
    /// carries the whole body, for the reason given on the class.
    /// </remarks>
    public ValueTask PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return _broadcasts.Get(@event.GetType())
            .PublishPooled(@event, _serviceProvider, cancellationToken, null);
    }


    /// <summary>
    /// Publishes <paramref name="event"/> under a canonical group set.
    /// </summary>
    /// <param name="event">The event to publish.</param>
    /// <param name="groups">
    /// The groups to deliver to. An empty set publishes through the default pipeline.
    /// </param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>A task that completes when every matching handler has run.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="event"/> or <paramref name="groups"/> is <c>null</c>.
    /// </exception>
    public ValueTask PublishAsync(IEvent @event, GroupSet groups, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(groups);

        return _broadcasts.Get(@event.GetType()).PublishPooled(
            @event, _serviceProvider, cancellationToken,
            groups.Count == 0 ? null : groups);
    }

    /// <summary>
    /// Publishes <paramref name="event"/> through its default pipeline, naming its type at
    /// compile time.
    /// </summary>
    /// <typeparam name="TEvent">The event's compile-time type.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="event"/> is <c>null</c>.</exception>
    public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(@event);

        var dispatch = @event.GetType() == typeof(TEvent)
            ? _broadcasts.Get<TEvent>()
            : _broadcasts.Get(@event.GetType());

        return dispatch.PublishPooled(@event, _serviceProvider, cancellationToken, null);
    }


    /// <summary>
    /// Publishes <paramref name="event"/> under a canonical group set, naming its type at
    /// compile time.
    /// </summary>
    /// <typeparam name="TEvent">The event's compile-time type.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="groups">
    /// The groups to deliver to. An empty set publishes through the default pipeline.
    /// </param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>A task that completes when every matching handler has run.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="event"/> or <paramref name="groups"/> is <c>null</c>.
    /// </exception>
    public ValueTask PublishAsync<TEvent>(TEvent @event, GroupSet groups, CancellationToken cancellationToken = default)
        where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(groups);

        var dispatch = @event.GetType() == typeof(TEvent)
            ? _broadcasts.Get<TEvent>()
            : _broadcasts.Get(@event.GetType());

        return dispatch.PublishPooled(
            @event, _serviceProvider, cancellationToken,
            groups.Count == 0 ? null : groups);
    }
}
