using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;

/// <summary>
/// Runs after every handler of an event that implements <see cref="IEvent"/> has been
/// delivered to.
/// </summary>
/// <remarks>
/// A publish produces no result, so there is nothing here to read or replace — this stage
/// exists to act on the fact that delivery finished.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventPostInterceptor : IEvent, IAsyncPostInterceptor<IEvent>
{
    /// <summary>
    /// Forwards the core contract to the method below.
    /// </summary>
    /// <param name="event">The event that was delivered.</param>
    /// <param name="result">Ignored; a publish has no result.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <returns>The value a resultless pipeline carries.</returns>
    async ValueTask<object> IAsyncPostInterceptor<IEvent>.HandleAsync(IEvent @event, object result, ErgosfareContext context)
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
    ValueTask HandleAsync(IEvent @event, ValueTask result, ErgosfareContext executionContext);
}
