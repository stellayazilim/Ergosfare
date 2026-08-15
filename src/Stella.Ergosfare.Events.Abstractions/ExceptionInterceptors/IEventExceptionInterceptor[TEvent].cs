using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;


/// <summary>
/// Represents an asynchronous exception interceptor for events, allowing custom logic
/// to execute when an exception occurs during event handling.
/// </summary>
/// <typeparam name="TEvent">The type of event being intercepted. Must be non-nullable and implement <see cref="IEvent"/>.</typeparam>
/// <remarks>
/// <para>
/// Implementing this interface allows the interceptor to participate in the event mediation
/// pipeline when an exception is thrown during the handling of <typeparamref name="TEvent"/>.
/// </para>
/// <para>
/// A publish produces no result, so this member takes none. The pipeline's resultless slot
/// is an implementation detail of the stage machinery and never carried anything an
/// interceptor could read — it only ever held a completed task.
/// </para>
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventExceptionInterceptor<in TEvent> : IEvent, IAsyncExceptionInterceptor<TEvent, Unit>
    where TEvent : notnull
{
    /// <inheritdoc cref="IAsyncExceptionInterceptor{TEvent, TResult}.HandleAsync"/>
    /// <remarks>
    /// A publish has no result: the slot carries <see cref="Unit.Value"/> and is not
    /// forwarded to the typed member below.
    /// </remarks>
    async ValueTask<object?> IAsyncExceptionInterceptor<TEvent, Unit>.HandleAsync(TEvent @event, Unit? result,
        Exception exception, ErgosfareContext context)
    {
        await HandleAsync(@event, exception, context);
        return Unit.Value;
    }

    /// <summary>
    /// Handles an exception asynchronously that occurred during the processing of the event.
    /// </summary>
    /// <param name="event">The event being processed.</param>
    /// <param name="exception">The exception thrown during event handling.</param>
    /// <param name="context">The execution context for the current mediation pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous exception handling operation.</returns>
    ValueTask HandleAsync(TEvent @event, Exception exception, ErgosfareContext context);
}
