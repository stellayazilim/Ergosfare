using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions;

namespace Ergosfare.Queries.Abstractions;

public interface IQueryMediator: IMessage
{
    /// <summary>
    ///     Asynchronously executes a query and returns the result.
    /// </summary>
    /// <typeparam name="TQueryResult">The type of the result returned by the query.</typeparam>
    /// <param name="query">The query to be executed.</param>
    /// <param name="queryMediationSettings">
    ///     Optional settings for query mediation that control aspects such as handler
    ///     filtering.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the operation that can be used to cancel the query processing.</param>
    /// <returns>A task representing the asynchronous operation with a result of type <typeparamref name="TQueryResult" />.</returns>
    /// <remarks>
    ///     This method is used for queries that produce a single result of type <typeparamref name="TQueryResult" />.
    ///     The query is routed to its appropriate handler based on its type, and the query handling pipeline
    ///     is executed, including pre-handlers, the main handler, post-handlers, and error handlers if exceptions occur.
    ///     The result produced by the handler is returned to the caller.
    /// </remarks>
    Task<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query,
                                                QueryMediationSettings? queryMediationSettings = null,
                                                CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously streams the results of a query.
    /// </summary>
    /// <typeparam name="TQueryResult">The type of the results returned by the stream query.</typeparam>
    /// <param name="query">The stream query to be executed.</param>
    /// <param name="queryMediationSettings">
    ///     Optional settings for query mediation that control aspects such as handler
    ///     filtering.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the operation that can be used to cancel the query processing.</param>
    /// <returns>An async enumerable of results of type <typeparamref name="TQueryResult" />.</returns>
    /// <remarks>
    ///     This method is used for stream queries that produce a sequence of results of type
    ///     <typeparamref name="TQueryResult" />.
    ///     Stream queries are particularly useful for retrieving large datasets, implementing pagination,
    ///     or handling real-time data streams.
    ///     The query is routed to its appropriate handler based on its type, and the query handling pipeline
    ///     is executed, including pre-handlers, the main handler, post-handlers, and error handlers if exceptions occur.
    ///     The sequence of results produced by the handler is returned to the caller as an <see cref="IAsyncEnumerable{T}" />,
    ///     allowing for asynchronous enumeration of the results.
    /// </remarks>
    IAsyncEnumerable<TQueryResult> StreamAsync<TQueryResult>(IStreamQuery<TQueryResult> query,
                                                             QueryMediationSettings? queryMediationSettings = null,
                                                             CancellationToken cancellationToken = default);
}