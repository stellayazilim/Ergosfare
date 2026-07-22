using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;


/// <summary>
/// Represents a type-safe exception interceptor for queries with a strongly-typed result.
/// The interceptor can inspect the exception and modify or replace the query result.
/// </summary>
/// <typeparam name="TQuery">The query type being intercepted. Must implement <see cref="IQuery{TResult}"/>.</typeparam>
/// <typeparam name="TResult">
/// The result type of the query. Also the type returned by the interceptor — for a
/// narrower return type there is no third parameter anymore; return the base result type.
/// </typeparam>
/// <remarks>
/// The type parameters are deliberately invariant: the pipeline invokes interceptors
/// through the non-generic root interfaces, so interface variance bought nothing while
/// forcing the result-returning member onto a separate three-parameter interface.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IQueryExceptionInterceptor<TQuery, TResult>
    : IQuery, IAsyncExceptionInterceptor<TQuery, TResult>
    where TQuery : IQuery<TResult>
    where TResult : notnull
{
    /// <inheritdoc />
    async Task<object?> IAsyncExceptionInterceptor<TQuery, TResult>.HandleAsync(
        TQuery query, TResult? result, Exception exception, IExecutionContext context)
        => await HandleAsync(query, result, exception, context);

    /// <summary>
    /// Handles the exception asynchronously, potentially modifying the query result.
    /// </summary>
    /// <param name="query">The query being processed when the exception occurred.</param>
    /// <param name="result">The result produced before the exception occurred, if any.</param>
    /// <param name="exception">The exception thrown during pipeline execution.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> producing the (possibly modified) result that
    /// continues through the pipeline.
    /// </returns>
    new Task<TResult?> HandleAsync(TQuery query, TResult? result, Exception exception, IExecutionContext context);
}
