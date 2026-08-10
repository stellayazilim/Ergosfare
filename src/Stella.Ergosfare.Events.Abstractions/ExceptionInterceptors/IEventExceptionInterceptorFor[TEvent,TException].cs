using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;


/// <summary>
/// An exception interceptor for a specific event type that runs only for exceptions of type
/// <typeparamref name="TException"/>. The exception arrives already typed — no <c>is</c>
/// check in the interceptor body.
/// </summary>
/// <typeparam name="TEvent">The type of event being intercepted. Must implement <see cref="IEvent"/>.</typeparam>
/// <typeparam name="TException">
/// The exception type this interceptor accepts, matched with <c>catch</c> semantics:
/// derived exception types match too.
/// </typeparam>
/// <remarks>
/// A publish produces no result, so — unlike
/// <see cref="IEventExceptionInterceptor{TEvent}"/>, which still carries a vestigial
/// <see cref="ValueTask"/> parameter — the handled member takes only the event, the
/// exception and the context. When no interceptor accepts the thrown exception, it leaves
/// the pipeline unwrapped with its original stack.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventExceptionInterceptorFor<in TEvent, TException> :
    IEvent, IAsyncExceptionInterceptor<TEvent, Unit>, IExceptionInterceptorFilter<TException>
    where TEvent : notnull
    where TException : Exception
{
    /// <inheritdoc />
    async ValueTask<object?> IAsyncExceptionInterceptor<TEvent, Unit>.HandleAsync(
        TEvent @event, Unit? result, Exception exception, IExecutionContext context)
    {
        // The cast cannot fail: the exception stage runs this interceptor only after its
        // filter accepted the exception.
        await HandleAsync(@event, (TException)exception, context);
        return Unit.Value;
    }

    /// <summary>
    /// Handles an exception thrown while the event was being published.
    /// </summary>
    /// <param name="event">The event being processed when the exception occurred.</param>
    /// <param name="exception">The exception thrown during pipeline execution.</param>
    /// <param name="context">The execution context for the current mediation pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous exception handling operation.</returns>
    ValueTask HandleAsync(TEvent @event, TException exception, IExecutionContext context);
}
