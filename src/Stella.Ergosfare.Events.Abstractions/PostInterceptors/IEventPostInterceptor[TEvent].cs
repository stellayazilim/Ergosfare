using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;

/// <summary>
/// Runs after every handler of a <typeparamref name="TEvent"/> has been delivered to.
/// </summary>
/// <typeparam name="TEvent">
/// The event type this interceptor accepts. Any non-null type will do — an event need not
/// implement <see cref="IEvent"/>.
/// </typeparam>
/// <remarks>
/// A publish produces no result, so there is nothing here to read or replace — this stage
/// exists to act on the fact that delivery finished.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventPostInterceptor<in TEvent> : IEvent, IAsyncPostInterceptor<TEvent> where TEvent : notnull
{
    /// <summary>
    /// Forwards the core contract to the typed method below.
    /// </summary>
    /// <param name="event">The event that was delivered.</param>
    /// <param name="result">Ignored; a publish has no result.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <returns>The value a resultless pipeline carries.</returns>
    async ValueTask<object> IAsyncPostInterceptor<TEvent>.HandleAsync(TEvent @event, object result, ErgosfareContext context)
    {
        // The ValueTask argument is what binds this to the typed member below. An
        // object-typed argument would resolve back to the inherited interface member — this
        // very implementation — and recurse forever.
        await HandleAsync(@event, ValueTask.CompletedTask, context);
        return Unit.Value;
    }

    /// <summary>
    /// Runs once the event has been delivered to every handler.
    /// </summary>
    /// <param name="event">The event that was delivered.</param>
    /// <param name="result">
    /// A completed task, standing in for a result a publish does not have. It carries no
    /// information.
    /// </param>
    /// <param name="executionContext">The execution context of this publish.</param>
    /// <returns>A task that completes when the interceptor is done.</returns>
    ValueTask HandleAsync(TEvent @event, ValueTask result, ErgosfareContext executionContext);
}
