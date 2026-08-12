using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;


/// <summary>
/// A module-wide exception interceptor that runs for every query but only for exceptions of
/// type <typeparamref name="TException"/> — the filtered form of
/// <see cref="IQueryExceptionInterceptor"/>, and the shape a global error policy takes.
/// </summary>
/// <typeparam name="TException">
/// The exception type this interceptor accepts, matched with <c>catch</c> semantics:
/// derived exception types match too.
/// </typeparam>
/// <remarks>
/// Being message- and result-agnostic, this interceptor joins the exception stage of every
/// query pipeline in the module; the filter is what keeps it from swallowing exceptions it
/// was not written for.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IQueryExceptionInterceptorFor<TException> :
    IQuery, IAsyncExceptionInterceptor<IQuery>, IExceptionInterceptorFilter<TException>
    where TException : Exception
{
    /// <inheritdoc />
    async ValueTask<object> IAsyncExceptionInterceptor<IQuery>.HandleAsync(
        IQuery query, object? messageResult, Exception exception, ErgosfareContext context)
        // The cast cannot fail: the exception stage runs this interceptor only after its
        // filter accepted the exception.
        => await HandleAsync(query, messageResult, (TException)exception, context);

    /// <summary>
    /// Handles the exception asynchronously, potentially replacing the pipeline result.
    /// </summary>
    /// <param name="query">The query being processed when the exception occurred.</param>
    /// <param name="messageResult">The result produced before the exception occurred, if any.</param>
    /// <param name="exception">The exception thrown during pipeline execution.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A <see cref="ValueTask{Object}"/> producing the result that continues through the pipeline.
    /// </returns>
    ValueTask<object> HandleAsync(IQuery query, object? messageResult, TException exception, ErgosfareContext context);
}
