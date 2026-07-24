using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;

/// <summary>
/// Represents a type-safe pre-interceptor for events. It runs before the event handlers and
/// returns the event that continues through the pipeline — the original, or a rewritten one.
/// </summary>
/// <typeparam name="TEvent">The type of event being intercepted.</typeparam>
/// <remarks>
/// A pre-interceptor carries no result, so the single-parameter form returns the event type
/// directly rather than <see cref="object"/>. Use the non-generic
/// <see cref="IEventPreInterceptor"/> to intercept any event, or
/// <see cref="IEventPreInterceptor{TEvent, TModifiedEvent}"/> to return a different, derived
/// event type. <typeparamref name="TEvent"/> is invariant because it is returned.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventPreInterceptor<TEvent> : IEvent, IAsyncPreInterceptor<TEvent>
    where TEvent : notnull
{
    /// <inheritdoc cref="IAsyncPreInterceptor{TMessage}.HandleAsync(TMessage,IExecutionContext)"/>
    async ValueTask<object> IAsyncPreInterceptor<TEvent>.HandleAsync(TEvent @event, IExecutionContext context)
        => await HandleAsync(@event, context);

    /// <summary>
    /// Handles the event before its handlers run and returns the event that continues through
    /// the pipeline (the original, or a rewritten instance).
    /// </summary>
    /// <param name="event">The event to intercept.</param>
    /// <param name="context">The current execution context.</param>
    new ValueTask<TEvent> HandleAsync(TEvent @event, IExecutionContext context);
}
