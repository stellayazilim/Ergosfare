using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;


/// <summary>
/// Represents a type-safe post-interceptor for queries with a strongly-typed result.
/// Executes after the main query handler has completed and can modify the result before
/// it propagates further through the pipeline.
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
public interface IQueryPostInterceptor<TQuery, TResult> : IQuery, IAsyncPostInterceptor<TQuery, TResult>
    where TQuery : IQuery<TResult>
    where TResult : notnull
{
    /// <inheritdoc />
    async Task<object> IAsyncPostInterceptor<TQuery, TResult>.HandleAsync(
        TQuery query, TResult messageResult, IExecutionContext context)
        => (await HandleAsync(query, messageResult, context))!;

    /// <summary>
    /// Handles the post-processing of a query asynchronously.
    /// </summary>
    /// <param name="query">The query that was executed.</param>
    /// <param name="queryResult">The result produced by the query handler.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> producing the (possibly modified) result that
    /// continues through the pipeline.
    /// </returns>
    new Task<TResult> HandleAsync(TQuery query, TResult queryResult, IExecutionContext context);
}
