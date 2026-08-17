using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;

/// <summary>
/// Runs once the pipeline of a <typeparamref name="TEvent"/> has settled, whether delivery
/// succeeded or failed.
/// </summary>
/// <typeparam name="TEvent">
/// The event type this interceptor accepts. Unlike the other event interceptors, this one
/// requires the event to implement <see cref="IEvent"/>.
/// </typeparam>
/// <remarks>
/// It observes the outcome and cannot change it, and a publish stopped by
/// <c>context.Abort()</c> runs no final interceptors. Because a publish has no result, the
/// only outcome to observe is the failure.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventFinalInterceptor<in TEvent> : IEvent, IAsyncFinalInterceptor<TEvent, Unit>
    where TEvent : IEvent
{
    /// <summary>
    /// Forwards the core contract to the typed method below, dropping the empty result.
    /// </summary>
    /// <param name="message">The event that was published.</param>
    /// <param name="result">Ignored; a publish has no result.</param>
    /// <param name="exception">The failure that ended the publish, if it failed.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <returns>A task that completes when the interceptor is done.</returns>
    ValueTask IAsyncFinalInterceptor<TEvent, Unit>.HandleAsync(TEvent message, Unit? result,
        Exception? exception, ErgosfareContext context)
        => HandleAsync(message, exception, context);

    /// <summary>
    /// Observes how the publish settled.
    /// </summary>
    /// <param name="event">The event that was published.</param>
    /// <param name="exception">
    /// The failure that ended the publish, or <c>null</c> when it succeeded.
    /// </param>
    /// <param name="context">The execution context of this publish.</param>
    /// <returns>A task that completes when the interceptor is done.</returns>
    ValueTask HandleAsync(TEvent @event, Exception? exception, ErgosfareContext context);
}
