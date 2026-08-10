using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;


/// <summary>
/// A result-agnostic exception interceptor for a specific query type that runs only for
/// exceptions of type <typeparamref name="TException"/>. The exception arrives already
/// typed — no <c>is</c> check in the interceptor body.
/// </summary>
/// <typeparam name="TQuery">The type of query being intercepted. Must implement <see cref="IQuery"/>.</typeparam>
/// <typeparam name="TException">
/// The exception type this interceptor accepts, matched with <c>catch</c> semantics:
/// derived exception types match too.
/// </typeparam>
/// <remarks>
/// The result-agnostic base keeps the interceptor visible to the pipeline's pattern match
/// whatever the query's result type is, including value-typed results. For a strongly-typed
/// result use <see cref="IQueryExceptionInterceptorFor{TQuery, TResult, TException}"/>.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IQueryExceptionInterceptorFor<in TQuery, TException> :
    IQuery, IAsyncExceptionInterceptor<TQuery>, IExceptionInterceptorFilter<TException>
    where TQuery : IQuery
    where TException : Exception
{
    /// <inheritdoc />
    async ValueTask<object> IAsyncExceptionInterceptor<TQuery>.HandleAsync(
        TQuery query, object? messageResult, Exception exception, IExecutionContext context)
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
    ValueTask<object> HandleAsync(TQuery query, object? messageResult, TException exception, IExecutionContext context);
}
