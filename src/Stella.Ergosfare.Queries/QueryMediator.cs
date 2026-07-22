using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Queries;


/// <summary>
/// The default implementation of <see cref="IQueryMediator"/>.
/// Handles both standard queries and streaming queries using the internal message mediation pipeline,
/// supporting pre/post/final interceptors and result adapters.
/// </summary>
public class QueryMediator(
    ActualTypeOrFirstAssignableTypeMessageResolveStrategy messageResolveStrategy,
    IMessageMediator messageMediator): IQueryMediator
{

    /// <summary>
    /// Executes a query and returns a single result of type <typeparamref name="TResult"/>.
    /// The query is processed through the mediation pipeline, including pre/post/final interceptors.
    /// </summary>
    /// <typeparam name="TResult">The expected result type of the query.</typeparam>
    /// <param name="query">The query message to process.</param>
    /// <param name="queryMediationSettings">
    /// Optional settings to influence pipeline execution, such as filters and custom items.
    /// </param>
    /// <param name="cancellationToken">A cancellation token for async execution.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous execution of the query.</returns>
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, QueryMediationSettings? queryMediationSettings = null,
        CancellationToken cancellationToken = default)
    {
        return messageMediator.DispatchAsync<TResult>(
            query,
            queryMediationSettings?.Items,
            cancellationToken,
            queryMediationSettings?.Filters.Groups);
    }

    
    /// <summary>
    /// Executes a streaming query and returns an asynchronous enumerable of results.
    /// The query is processed through the streaming pipeline, supporting interceptors and result adapters.
    /// </summary>
    /// <typeparam name="TResult">The type of elements produced by the stream query.</typeparam>
    /// <param name="query">The streaming query to execute.</param>
    /// <param name="queryMediationSettings">
    /// Optional settings to influence pipeline execution, such as filters and custom items.
    /// </param>
    /// <param name="cancellationToken">A cancellation token for async streaming.</param>
    /// <returns>An <see cref="IAsyncEnumerable{TResult}"/> representing the results of the streaming query.</returns>
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query, QueryMediationSettings? queryMediationSettings = null,
        CancellationToken cancellationToken = default)
    {
        return QueryStreamInvokerCache.Get<TResult>(query.GetType()).Stream(
            query, queryMediationSettings, cancellationToken, messageMediator, messageResolveStrategy);
    }
}