using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;


/// <summary>
/// Exception interceptor for every event: the untyped counterpart of
/// <see cref="IEventExceptionInterceptor{TEvent}"/>, reached for any published message.
/// </summary>
/// <remarks>
/// Carries its own member rather than inheriting the stage contract's, so the resultless
/// slot the machinery threads never reaches an implementor — a publish has no result, and a
/// parameter that can only ever hold one fixed value is not a parameter.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventExceptionInterceptor : IEvent, IAsyncExceptionInterceptor<IEvent, Unit>
{
    /// <inheritdoc cref="IAsyncExceptionInterceptor{TEvent, TResult}.HandleAsync"/>
    async ValueTask<object?> IAsyncExceptionInterceptor<IEvent, Unit>.HandleAsync(IEvent @event, Unit? result,
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
    ValueTask HandleAsync(IEvent @event, Exception exception, ErgosfareContext context);
}
