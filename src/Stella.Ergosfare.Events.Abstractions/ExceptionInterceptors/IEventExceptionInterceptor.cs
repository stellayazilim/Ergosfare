using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;

/// <summary>
/// Handles failures raised while publishing an event that implements <see cref="IEvent"/>.
/// </summary>
/// <remarks>
/// Running is what marks the failure handled, and a failure no interceptor accepts reaches
/// the publisher unchanged. Because a publish has no result, there is nothing to produce —
/// handling here means the publish completes rather than throwing. Use
/// <see cref="IEventExceptionInterceptorFor{TException}"/> to accept only certain failures.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventExceptionInterceptor : IEvent, IAsyncExceptionInterceptor<IEvent, Unit>
{
    /// <summary>
    /// Forwards the core contract to the method below.
    /// </summary>
    /// <param name="event">The event whose publish failed.</param>
    /// <param name="result">Ignored; a publish has no result.</param>
    /// <param name="exception">The failure being handled.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <returns>The value a resultless pipeline carries.</returns>
    async ValueTask<object?> IAsyncExceptionInterceptor<IEvent, Unit>.HandleAsync(IEvent @event, Unit? result,
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
    ValueTask HandleAsync(IEvent @event, Exception exception, ErgosfareContext context);
}
