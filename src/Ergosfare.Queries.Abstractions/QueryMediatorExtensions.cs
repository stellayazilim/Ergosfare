using Ergosfare.Contracts;

namespace Ergosfare.Queries.Abstractions;

/// <summary>
/// Provides extension methods for <see cref="IQueryMediator"/> to simplify dispatching
/// queries and stream queries with optional group filtering.
/// </summary>
public static class QueryMediatorExtensions
{
    /// <summary>
    /// Dispatches a query asynchronously and returns its result.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the query.</typeparam>
    /// <param name="queryMediator">The query mediator used to handle the query.</param>
    /// <param name="query">The query to be dispatched.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation.</returns>
    public static Task<TResult> QueryAsync<TResult>(
        this IQueryMediator queryMediator,
        IQuery<TResult> query,
        CancellationToken cancellationToken = default
    )
    {
        return queryMediator.QueryAsync(query, null, cancellationToken);
    }


    
    /// <summary>
    /// Dispatches a query asynchronously and returns its result, with optional group filtering.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the query.</typeparam>
    /// <param name="queryMediator">The query mediator used to handle the query.</param>
    /// <param name="query">The query to be dispatched.</param>
    /// <param name="groups">An array of groups to filter which handlers are invoked.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation.</returns>
    public static Task<TResult> QueryAsync<TResult>(
        this IQueryMediator queryMediator,
        IQuery<TResult> query,
        string[] groups,
        CancellationToken cancellationToken = default)
    {
        return queryMediator.QueryAsync(
            query, new QueryMediationSettings()
            {
                Filters = { Groups = groups }
            }, cancellationToken );
    }
    
    
    
    /// <summary>
    /// Dispatches a stream query asynchronously and returns an async enumerable of results.
    /// </summary>
    /// <typeparam name="TResult">The type of results produced by the stream query.</typeparam>
    /// <param name="queryMediator">The query mediator used to handle the stream query.</param>
    /// <param name="query">The stream query to be dispatched.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{TResult}"/> producing results asynchronously.</returns>
    public static IAsyncEnumerable<TResult> StreamAsync<TResult>(
        this IQueryMediator queryMediator,
        IStreamQuery<TResult> query,
        CancellationToken cancellationToken = default
    )
    {
        return queryMediator.StreamAsync(query, null, cancellationToken);
    }


    /// <summary>
    /// Dispatches a stream query asynchronously and returns an async enumerable of results,
    /// with optional group filtering.
    /// </summary>
    /// <typeparam name="TResult">The type of results produced by the stream query.</typeparam>
    /// <param name="queryMediator">The query mediator used to handle the stream query.</param>
    /// <param name="query">The stream query to be dispatched.</param>
    /// <param name="groups">An array of groups to filter which handlers are invoked.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{TResult}"/> producing results asynchronously.</returns>
    public static IAsyncEnumerable<TResult> StreamAsync<TResult>(
        this IQueryMediator queryMediator,
        IStreamQuery<TResult> query,
        string[] groups,
        CancellationToken cancellationToken = default
    )
    {
        return queryMediator.StreamAsync(query, new QueryMediationSettings()
        {
            Filters = { Groups = groups }
        }, cancellationToken );
    } 
}