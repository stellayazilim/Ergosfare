using System.Collections.Concurrent;
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
    IResultAdapterService resultAdapterService,
    IMessageMediator messageMediator): IQueryMediator
{
    private static readonly string[] EmptyGroups = [];

    /// <summary>
    /// Mediation strategies for queries, one per result type; stateless, so shared across
    /// calls. Created lazily so mediators in short-lived scopes that never run typed
    /// queries pay nothing.
    /// </summary>
    private ConcurrentDictionary<Type, object>? _typedMediationStrategies;

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
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous execution of the query.</returns>
    public Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, QueryMediationSettings? queryMediationSettings = null,
        CancellationToken cancellationToken = default)
    {
        // Reuse the mediation strategy for this result type
        var typedMediationStrategies = LazyInitializer.EnsureInitialized(ref _typedMediationStrategies);

        if (!typedMediationStrategies.TryGetValue(typeof(TResult), out var mediationStrategy))
        {
            mediationStrategy = typedMediationStrategies.GetOrAdd(typeof(TResult),
                new SingleAsyncHandlerMediationStrategy<IQuery<TResult>, TResult>(resultAdapterService));
        }

        // Execute the query through the message mediator
        var result = messageMediator.Mediate(query,
            new MediateOptions<IQuery<TResult>, Task<TResult>>
            {
                MessageMediationStrategy = (SingleAsyncHandlerMediationStrategy<IQuery<TResult>, TResult>)mediationStrategy,
                MessageResolveStrategy = messageResolveStrategy,
                CancellationToken = cancellationToken,
                Items = queryMediationSettings?.Items,
                Groups = queryMediationSettings?.Filters.Groups ?? EmptyGroups
            });
        return result;
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
        // Build a mediation strategy for streaming queries (carries the per-call cancellation token)
        var mediationStrategy = new SingleStreamHandlerMediationStrategy<IStreamQuery<TResult>, TResult>(new ResultAdapterService(), cancellationToken);
        // Execute the streaming query through the message mediator
        var result =  messageMediator.Mediate(query,
            new MediateOptions<IStreamQuery<TResult>, IAsyncEnumerable<TResult>>
            {
                MessageMediationStrategy = mediationStrategy,
                MessageResolveStrategy = messageResolveStrategy,
                CancellationToken = cancellationToken,
                Items = queryMediationSettings?.Items,
                Groups = queryMediationSettings?.Filters.Groups ?? EmptyGroups
            });
        return result;
    }
}