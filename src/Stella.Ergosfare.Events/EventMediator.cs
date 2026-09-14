using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events;


/// <summary>
/// The event mediator an application resolves: it holds the scope it was resolved from and
/// the container's publish pipelines.
/// </summary>
public class EventMediator : IPublisher
{
    /// <summary>
    /// The engine that invokes generated broadcast plans.
    /// </summary>
    private readonly MessageDispatchEngine _engine;

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
    internal EventMediator(
        MessageDispatchEngine engine,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _engine = engine;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public ValueTask PublishAsync(IEvent @event, GroupSet groups,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return _engine.BroadcastAsync(@event, _serviceProvider, cancellationToken, groups ?? throw new ArgumentNullException(nameof(groups)));
    }

    /// <inheritdoc />
    public ValueTask PublishAsync<TEvent>(TEvent @event, GroupSet groups,
        CancellationToken cancellationToken = default)
        where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(@event);

        return _engine.BroadcastAsync(@event, _serviceProvider, cancellationToken, groups ?? throw new ArgumentNullException(nameof(groups)));
    }

    /// <inheritdoc />
    public ValueTask PublishAsync(IEvent @event, ErgosfareContext context, GroupSet? groups = null)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);

        return _engine.BroadcastAsync(@event, context, _serviceProvider, groups);
    }

    /// <summary>
    /// Publishes <paramref name="event"/> through its default pipeline.
    /// </summary>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="event"/> is <c>null</c>.</exception>
    public ValueTask PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return _engine.BroadcastAsync(@event, _serviceProvider, cancellationToken, null);
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


        return _engine.BroadcastAsync(@event, _serviceProvider, cancellationToken, null);
    }


}
