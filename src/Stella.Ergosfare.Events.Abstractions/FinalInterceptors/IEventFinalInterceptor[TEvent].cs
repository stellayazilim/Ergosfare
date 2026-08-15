using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;


/// <summary>
/// Final interceptor for <typeparamref name="TEvent"/>: runs after the publish settles,
/// whether it succeeded or failed, and is skipped only by an abort.
/// </summary>
/// <typeparam name="TEvent">The type of event being intercepted.</typeparam>
/// <remarks>
/// Carries its own member rather than inheriting the stage contract's, so the resultless
/// slot the machinery threads never reaches an implementor — a publish has no result, and a
/// parameter that can only ever hold one fixed value is not a parameter.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventFinalInterceptor<in TEvent> : IEvent, IAsyncFinalInterceptor<TEvent, Unit>
    where TEvent : IEvent
{
    /// <inheritdoc cref="IAsyncFinalInterceptor{TMessage, TResult}.HandleAsync"/>
    ValueTask IAsyncFinalInterceptor<TEvent, Unit>.HandleAsync(TEvent message, Unit? result,
        Exception? exception, ErgosfareContext context)
        => HandleAsync(message, exception, context);

    /// <summary>
    /// Runs after the publish has settled.
    /// </summary>
    /// <param name="event">The event that was published.</param>
    /// <param name="exception">The failure the publish ended with, or <c>null</c> when it succeeded.</param>
    /// <param name="context">The execution context for the current mediation pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask HandleAsync(TEvent @event, Exception? exception, ErgosfareContext context);
}
