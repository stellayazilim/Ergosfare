using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;


/// <summary>
/// A module-wide exception interceptor that runs for every event but only for exceptions of
/// type <typeparamref name="TException"/> — the filtered form of
/// <see cref="IEventExceptionInterceptor"/>, and the shape a global error policy takes.
/// </summary>
/// <typeparam name="TException">
/// The exception type this interceptor accepts, matched with <c>catch</c> semantics:
/// derived exception types match too.
/// </typeparam>
/// <remarks>
/// Being message-agnostic, this interceptor joins the exception stage of every event
/// pipeline in the module; the filter is what keeps it from swallowing exceptions it was
/// not written for.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventExceptionInterceptorFor<TException> :
    IEvent, IAsyncExceptionInterceptor<IEvent, Unit>, IExceptionInterceptorFilter<TException>
    where TException : Exception
{
    /// <inheritdoc />
    async ValueTask<object?> IAsyncExceptionInterceptor<IEvent, Unit>.HandleAsync(
        IEvent @event, Unit? result, Exception exception, IExecutionContext context)
    {
        // The cast cannot fail: the exception stage runs this interceptor only after its
        // filter accepted the exception.
        await HandleAsync(@event, (TException)exception, context);
        return Unit.Value;
    }

    /// <summary>
    /// Handles an exception thrown while an event was being published.
    /// </summary>
    /// <param name="event">The event being processed when the exception occurred.</param>
    /// <param name="exception">The exception thrown during pipeline execution.</param>
    /// <param name="context">The execution context for the current mediation pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous exception handling operation.</returns>
    ValueTask HandleAsync(IEvent @event, TException exception, IExecutionContext context);
}
