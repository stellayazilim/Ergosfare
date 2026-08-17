using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;

/// <summary>
/// Handles failures of type <typeparamref name="TException"/> raised while publishing a
/// <typeparamref name="TEvent"/>.
/// </summary>
/// <typeparam name="TEvent">
/// The event type this interceptor accepts. Any non-null type will do — an event need not
/// implement <see cref="IEvent"/>.
/// </typeparam>
/// <typeparam name="TException">
/// The failure type this interceptor accepts. Matching follows <c>catch</c> semantics, so
/// derived types match too.
/// </typeparam>
/// <remarks>
/// The failure arrives already typed, so no type test is needed in the body. A failure this
/// interceptor rejects is left for another to accept, and one nothing accepts reaches the
/// publisher unchanged.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventExceptionInterceptorFor<in TEvent, TException> :
    IEvent, IAsyncExceptionInterceptor<TEvent, Unit>, IExceptionInterceptorFilter<TException>
    where TEvent : notnull
    where TException : Exception
{
    /// <summary>
    /// Forwards the core contract to the typed method below.
    /// </summary>
    /// <param name="event">The event whose publish failed.</param>
    /// <param name="result">Ignored; a publish has no result.</param>
    /// <param name="exception">The failure being handled.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <returns>The value a resultless pipeline carries.</returns>
    async ValueTask<object?> IAsyncExceptionInterceptor<TEvent, Unit>.HandleAsync(
        TEvent @event, Unit? result, Exception exception, ErgosfareContext context)
    {
        // The cast is safe: the stage only runs this interceptor once its filter accepted
        // the failure.
        await HandleAsync(@event, (TException)exception, context);
        return Unit.Value;
    }

    /// <summary>
    /// Handles <paramref name="exception"/>.
    /// </summary>
    /// <param name="event">The event whose publish failed.</param>
    /// <param name="exception">The failure being handled, already typed.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <returns>A task that completes when the interceptor is done.</returns>
    ValueTask HandleAsync(TEvent @event, TException exception, ErgosfareContext context);
}
