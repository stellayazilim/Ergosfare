using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;

/// <summary>
/// Runs before the handlers of a <typeparamref name="TEvent"/> and decides which event they
/// receive.
/// </summary>
/// <typeparam name="TEvent">
/// The event type this interceptor accepts. Any non-null type will do — an event need not
/// implement <see cref="IEvent"/>.
/// </typeparam>
/// <remarks>
/// The event returned is delivered to every handler, so replacing it here replaces it for
/// all of them. <typeparamref name="TEvent"/> is invariant because it is returned.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventPreInterceptor<TEvent> : IEvent, IAsyncPreInterceptor<TEvent>
    where TEvent : notnull
{
    /// <summary>
    /// Forwards the core contract to the typed method below.
    /// </summary>
    /// <param name="event">The event as the previous stage left it.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <returns>The event the typed method returned.</returns>
    async ValueTask<object> IAsyncPreInterceptor<TEvent>.HandleAsync(TEvent @event, ErgosfareContext context)
        => await HandleAsync(@event, context);

    /// <summary>
    /// Processes <paramref name="event"/> before its handlers run.
    /// </summary>
    /// <param name="event">The event as the previous stage left it.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <returns>
    /// The event the handlers receive — either the one passed in or a replacement.
    /// </returns>
    new ValueTask<TEvent> HandleAsync(TEvent @event, ErgosfareContext context);
}
