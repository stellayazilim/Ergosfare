using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Events.Abstractions;

/// <summary>
/// Publishes events to their handlers.
/// </summary>
/// <remarks>
/// An event reaches every handler registered for it, where a command reaches exactly one —
/// which is what lets parts of an application react to each other without knowing each
/// other. Everything a publish needs is passed as an argument; only the three abstract
/// members carry real work, and the rest are conveniences implemented in terms of them.
/// </remarks>
public interface IEventMediator
{
    /// <summary>
    /// Publishes <paramref name="event"/> to every handler registered for its type.
    /// </summary>
    /// <param name="event">The event to publish.</param>
    /// <param name="groups">
    /// The groups to deliver to; <c>null</c> uses the default group. Reusing a
    /// <see cref="GroupSet"/> lets the cached pipeline be matched by reference.
    /// </param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    ValueTask PublishAsync(IEvent @event, IEnumerable<string>? groups,
        CancellationToken cancellationToken);

    /// <summary>
    /// Publishes <paramref name="event"/> under an execution context supplied by the caller
    /// — the shape a nested publish uses.
    /// </summary>
    /// <param name="event">The event to publish.</param>
    /// <param name="context">
    /// The context to run under, typically a child opened with
    /// <c>using var scope = context.CreateScope();</c> and passed as <c>scope.Context</c>.
    /// The caller owns its lifetime, and cancellation comes from it.
    /// </param>
    /// <param name="groups">The groups to deliver to; <c>null</c> uses the default group.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    ValueTask PublishAsync(IEvent @event, ErgosfareContext context, IEnumerable<string>? groups = null);

    /// <summary>
    /// Publishes <paramref name="event"/> naming its type at compile time, which also
    /// allows any non-null type to be an event.
    /// </summary>
    /// <typeparam name="TEvent">The event's compile-time type.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="groups">The groups to deliver to; <c>null</c> uses the default group.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    /// <remarks>
    /// When the named type is the event's runtime type — the usual case — the pipeline is
    /// found through a compile-time slot rather than a lookup.
    /// </remarks>
    ValueTask PublishAsync<TEvent>(TEvent @event, IEnumerable<string>? groups,
        CancellationToken cancellationToken)
        where TEvent : notnull;

    /// <summary>
    /// Publishes <paramref name="event"/> through its default pipeline.
    /// </summary>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    ValueTask PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
        => PublishAsync(@event, (IEnumerable<string>?)null, cancellationToken);


    /// <summary>
    /// Publishes <paramref name="event"/> under a canonical group set.
    /// </summary>
    /// <param name="event">The event to publish.</param>
    /// <param name="groups">
    /// The groups to deliver to. Build the set once and reuse it, and the cached pipeline is
    /// matched by reference; <see cref="GroupSet.Empty"/> uses the default pipeline.
    /// </param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    ValueTask PublishAsync(IEvent @event, GroupSet groups, CancellationToken cancellationToken = default)
        => PublishAsync(@event, groups.Count == 0 ? null : (IEnumerable<string>?)groups, cancellationToken);

    /// <summary>
    /// Publishes <paramref name="event"/> through its default pipeline, naming its type at
    /// compile time.
    /// </summary>
    /// <typeparam name="TEvent">The event's compile-time type.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull
        => PublishAsync(@event, (IEnumerable<string>?)null, cancellationToken);


    /// <summary>
    /// Publishes <paramref name="event"/> under a canonical group set, naming its type at
    /// compile time.
    /// </summary>
    /// <typeparam name="TEvent">The event's compile-time type.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="groups">
    /// The groups to deliver to; <see cref="GroupSet.Empty"/> uses the default pipeline.
    /// </param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    ValueTask PublishAsync<TEvent>(TEvent @event, GroupSet groups, CancellationToken cancellationToken = default)
        where TEvent : notnull
        => PublishAsync(@event, groups.Count == 0 ? null : (IEnumerable<string>?)groups, cancellationToken);
}
