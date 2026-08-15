using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;


/// <summary>
/// Final interceptor for every event: the untyped counterpart of
/// <see cref="IEventFinalInterceptor{TEvent}"/>.
/// </summary>
/// <remarks>
/// Carries its own member rather than inheriting the stage contract's; see the typed
/// counterpart for why a publish's final stage takes no result.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventFinalInterceptor : IEvent, IAsyncFinalInterceptor<IEvent, Unit>
{
    /// <inheritdoc cref="IAsyncFinalInterceptor{TMessage, TResult}.HandleAsync"/>
    ValueTask IAsyncFinalInterceptor<IEvent, Unit>.HandleAsync(IEvent message, Unit? result,
        Exception? exception, ErgosfareContext context)
        => HandleAsync(message, exception, context);

    /// <summary>
    /// Runs after the publish has settled.
    /// </summary>
    /// <param name="event">The event that was published.</param>
    /// <param name="exception">The failure the publish ended with, or <c>null</c> when it succeeded.</param>
    /// <param name="context">The execution context for the current mediation pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask HandleAsync(IEvent @event, Exception? exception, ErgosfareContext context);
}
