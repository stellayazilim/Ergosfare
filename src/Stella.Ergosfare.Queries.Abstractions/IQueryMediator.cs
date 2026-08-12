using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Queries.Abstractions;

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
    ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query,
                                                QueryMediationSettings? queryMediationSettings = null,
                                                CancellationToken cancellationToken = default);

    /// <summary>
    ///     Executes a query under an externally owned execution context — the
    ///     nested-dispatch path: a handler opens a scope on its own context
    ///     (<c>using var scope = context.CreateScope();</c>) and passes
    ///     <c>scope.Context</c> here. The caller owns the context's lifetime;
    ///     cancellation flows from the context.
    /// </summary>
    /// <typeparam name="TQueryResult">The type of the result returned by the query.</typeparam>
    /// <param name="query">The query to be executed.</param>
    /// <param name="context">The externally owned execution context to dispatch under.</param>
    /// <param name="queryMediationSettings">Optional mediation settings (groups etc.).</param>
    ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query,
                                                ErgosfareContext context,
                                                QueryMediationSettings? queryMediationSettings = null);

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

    /// <summary>
    ///     Executes a query under a canonical group filter. With a reused
    ///     <see cref="GroupSet"/> (define filters once, statically) the grouped dispatch
    ///     caches match on a single reference check and the call allocates no settings
    ///     object. The default implementation routes through the settings overload, so
    ///     foreign mediator implementations keep working unchanged.
    /// </summary>
    /// <typeparam name="TQueryResult">The type of the result returned by the query.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="groups">The canonical group filter; <see cref="GroupSet.Empty"/> dispatches the default pipeline.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query, GroupSet groups,
        CancellationToken cancellationToken = default)
        => QueryAsync(query, new QueryMediationSettings { Filters = { Groups = groups } }, cancellationToken);

    /// <summary>
    ///     Streaming counterpart of
    ///     <see cref="QueryAsync{TQueryResult}(IQuery{TQueryResult}, GroupSet, CancellationToken)"/>.
    /// </summary>
    /// <typeparam name="TQueryResult">The type of the results returned by the stream query.</typeparam>
    /// <param name="query">The stream query to execute.</param>
    /// <param name="groups">The canonical group filter.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    IAsyncEnumerable<TQueryResult> StreamAsync<TQueryResult>(IStreamQuery<TQueryResult> query, GroupSet groups,
        CancellationToken cancellationToken = default)
        => StreamAsync(query, new QueryMediationSettings { Filters = { Groups = groups } }, cancellationToken);
}