using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;

/// <summary>
/// Runs before the handlers of any event that implements <see cref="IEvent"/>.
/// </summary>
/// <remarks>
/// This form observes the event without being able to replace it — the event that reaches
/// the handlers is the one that arrived. To replace it, implement
/// <see cref="IEventPreInterceptor{TEvent}"/>.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventPreInterceptor : IEvent, IAsyncPreInterceptor<IEvent>
{
    /// <summary>
    /// Forwards the core contract to the method below and passes the event on unchanged.
    /// </summary>
    /// <param name="event">The event as the previous stage left it.</param>
    /// <param name="executionContext">The execution context of this publish.</param>
    /// <returns>The event that arrived.</returns>
    async ValueTask<object> IAsyncPreInterceptor<IEvent>.HandleAsync(IEvent @event, ErgosfareContext executionContext)
    {
        await HandleAsync(@event, executionContext);
        return @event;
    }

    /// <summary>
    /// Processes <paramref name="event"/> before its handlers run.
    /// </summary>
    /// <param name="event">The event about to be delivered.</param>
    /// <param name="executionContext">The execution context of this publish.</param>
    /// <returns>A task that completes when the interceptor is done.</returns>
    new ValueTask HandleAsync(IEvent @event, ErgosfareContext executionContext);
}
