using Ergosfare.Contracts;
using Ergosfare.Core;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.Abstractions.SignalHub.Signals;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Queries.Abstractions;

namespace Ergosfare.Queries;


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
        // Trigger a pipeline event indicating that the query has started processing
        BeginPipelineSignal.Invoke(query, null);
        // Use default settings if none provided
        queryMediationSettings ??= new QueryMediationSettings();
        // Build a mediation strategy specific for single-result queries
        var mediationStrategy = new SingleAsyncHandlerMediationStrategy<IQuery<TResult>, TResult>(resultAdapterService);
        // Execute the query through the message mediator
        var result = messageMediator.Mediate(query,
            new MediateOptions<IQuery<TResult>, Task<TResult>>
            {
                MessageMediationStrategy = mediationStrategy,
                MessageResolveStrategy = messageResolveStrategy,
                CancellationToken = cancellationToken,
                Items = queryMediationSettings.Items,
                Groups = queryMediationSettings.Filters.Groups
            });
        // Trigger a pipeline event indicating that the query has completed
        FinishPipelineSignal.Invoke(query, result);
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
        // Trigger a pipeline event indicating that streaming has started
        BeginPipelineSignal.Invoke(query, null);
        // Use default settings if none provided
        queryMediationSettings ??= new QueryMediationSettings();
        // Build a mediation strategy for streaming queries
        var mediationStrategy = new SingleStreamHandlerMediationStrategy<IStreamQuery<TResult>, TResult>(new ResultAdapterService(), cancellationToken);
        // Execute the streaming query through the message mediator
        var result =  messageMediator.Mediate(query,
            new MediateOptions<IStreamQuery<TResult>, IAsyncEnumerable<TResult>>
            {
                MessageMediationStrategy = mediationStrategy,
                MessageResolveStrategy = messageResolveStrategy,
                CancellationToken = cancellationToken,
                Items = queryMediationSettings.Items,
                Groups = queryMediationSettings.Filters.Groups
            });
        // Trigger a pipeline event indicating that streaming has completed
        FinishPipelineSignal.Invoke(query, result);
        return result;
    }
}