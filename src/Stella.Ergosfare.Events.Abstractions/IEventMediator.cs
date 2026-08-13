using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Events.Abstractions;

/// <summary>
///     Represents the mediator interface for publishing events within the application.
/// </summary>
/// <remarks>
///     <para>
///         The event mediator is responsible for broadcasting events to all registered handlers
///         and orchestrating the event handling pipeline. Unlike commands, which are handled by
///         exactly one handler, events can be handled by multiple handlers, allowing for decoupled
///         communication between different parts of the application.
///     </para>
///     <para>
///         Everything a publish can be told is a parameter. A settings object used to carry the
///         same three things, and carrying them that way meant allocating one per publish and
///         reading it at dispatch time — a shape nothing can be compiled from. The conveniences
///         below are default implementations over the full call, so an implementation of this
///         interface writes three methods and inherits the rest.
///     </para>
/// </remarks>
public interface IEventMediator
{
    /// <summary>
    ///     Publishes an event to every handler registered for its type.
    /// </summary>
    /// <param name="event">The event to publish.</param>
    /// <param name="groups">
    ///     The group filter, or <c>null</c> for the default pipeline. A reused
    ///     <see cref="GroupSet"/> matches the cached pipeline on a single reference check.
    /// </param>
    /// <param name="throwIfNoHandlerFound">
    ///     Whether reaching nobody is an error. Left <c>false</c>, a publish nobody subscribes
    ///     to is a silent no-op — fire-and-forget being the point of an event.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    ValueTask PublishAsync(IEvent @event, IEnumerable<string>? groups, bool throwIfNoHandlerFound,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Publishes under an externally owned execution context — the nested-dispatch path: a
    ///     handler opens a scope on its own context (<c>using var scope = context.CreateScope();</c>)
    ///     and passes <c>scope.Context</c> here. The caller owns the context's lifetime;
    ///     cancellation flows from the context.
    /// </summary>
    ValueTask PublishAsync(IEvent @event, ErgosfareContext context, IEnumerable<string>? groups = null,
        bool throwIfNoHandlerFound = false);

    /// <summary>
    ///     Strongly-typed counterpart of
    ///     <see cref="PublishAsync(IEvent, IEnumerable{string}, bool, CancellationToken)"/>:
    ///     when the compile-time type is the event's runtime type, the pipeline comes from a
    ///     static-generic slot rather than a dictionary lookup.
    /// </summary>
    ValueTask PublishAsync<TEvent>(TEvent @event, IEnumerable<string>? groups, bool throwIfNoHandlerFound,
        CancellationToken cancellationToken)
        where TEvent : notnull;

    /// <summary>Publishes an event through its default pipeline.</summary>
    ValueTask PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
        => PublishAsync(@event, (IEnumerable<string>?)null, false, cancellationToken);


    /// <summary>
    ///     Publishes under a canonical group filter. Define the set once, statically, and the
    ///     cached pipeline matches it on a single reference check;
    ///     <see cref="GroupSet.Empty"/> publishes the default pipeline.
    /// </summary>
    ValueTask PublishAsync(IEvent @event, GroupSet groups, CancellationToken cancellationToken = default)
        => PublishAsync(@event, groups.Count == 0 ? null : (IEnumerable<string>?)groups, false, cancellationToken);

    /// <summary>Publishes under a group filter given as a plain array.</summary>
    ValueTask PublishAsync(IEvent @event, string[] groups, CancellationToken cancellationToken = default)
        => PublishAsync(@event, (IEnumerable<string>?)groups, false, cancellationToken);

    /// <summary>Typed counterpart of <see cref="PublishAsync(IEvent, CancellationToken)"/>.</summary>
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull
        => PublishAsync(@event, (IEnumerable<string>?)null, false, cancellationToken);


    /// <summary>Typed counterpart of <see cref="PublishAsync(IEvent, GroupSet, CancellationToken)"/>.</summary>
    ValueTask PublishAsync<TEvent>(TEvent @event, GroupSet groups, CancellationToken cancellationToken = default)
        where TEvent : notnull
        => PublishAsync(@event, groups.Count == 0 ? null : (IEnumerable<string>?)groups, false, cancellationToken);
}
