using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;

/// <summary>
/// Handles failures raised while publishing a <typeparamref name="TEvent"/>.
/// </summary>
/// <typeparam name="TEvent">
/// The event type this interceptor accepts. Any non-null type will do — an event need not
/// implement <see cref="IEvent"/>.
/// </typeparam>
/// <remarks>
/// Running is what marks the failure handled, and a failure no interceptor accepts reaches
/// the publisher unchanged. Because a publish has no result, handling here means the
/// publish completes rather than throwing. Use
/// <see cref="IEventExceptionInterceptorFor{TEvent, TException}"/> to accept only certain
/// failures.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventExceptionInterceptor<in TEvent> : IEvent, IAsyncExceptionInterceptor<TEvent, Unit>
    where TEvent : notnull
{
    /// <summary>
    /// Forwards the core contract to the typed method below.
    /// </summary>
    /// <param name="event">The event whose publish failed.</param>
    /// <param name="result">Ignored; a publish has no result.</param>
    /// <param name="exception">The failure being handled.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <returns>The value a resultless pipeline carries.</returns>
    async ValueTask<object?> IAsyncExceptionInterceptor<TEvent, Unit>.HandleAsync(TEvent @event, Unit? result,
        Exception exception, ErgosfareContext context)
    {
        await HandleAsync(@event, exception, context);
        return Unit.Value;
    }

    /// <summary>
    /// Handles <paramref name="exception"/>.
    /// </summary>
    /// <param name="event">The event whose publish failed.</param>
    /// <param name="exception">The failure being handled.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <returns>A task that completes when the interceptor is done.</returns>
    ValueTask HandleAsync(TEvent @event, Exception exception, ErgosfareContext context);
}
