using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;

/// <summary>
/// Runs once the pipeline of an event that implements <see cref="IEvent"/> has settled,
/// whether delivery succeeded or failed.
/// </summary>
/// <remarks>
/// It observes the outcome and cannot change it, and a publish stopped by
/// <c>context.Abort()</c> runs no final interceptors. Because a publish has no result, the
/// only outcome to observe is the failure, which is why the typed method takes just that.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventFinalInterceptor : IEvent, IAsyncFinalInterceptor<IEvent, Unit>
{
    /// <summary>
    /// Forwards the core contract to the method below, dropping the empty result.
    /// </summary>
    /// <param name="message">The event that was published.</param>
    /// <param name="result">Ignored; a publish has no result.</param>
    /// <param name="exception">The failure that ended the publish, if it failed.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <returns>A task that completes when the interceptor is done.</returns>
    ValueTask IAsyncFinalInterceptor<IEvent, Unit>.HandleAsync(IEvent message, Unit? result,
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
    ValueTask HandleAsync(IEvent @event, Exception? exception, ErgosfareContext context);
}
