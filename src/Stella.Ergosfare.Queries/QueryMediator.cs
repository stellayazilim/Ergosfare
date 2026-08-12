using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Queries;


/// <summary>
/// The default implementation of <see cref="IQueryMediator"/>.
/// Handles both standard queries and streaming queries using the internal message mediation pipeline,
/// supporting pre/post/final interceptors and result adapters.
/// </summary>
public class QueryMediator : IQueryMediator
{
    /// <summary>
    /// The singleton dispatch engine every query runs against.
    /// </summary>
    private readonly MessageDispatchEngine _engine;

    /// <summary>
    /// The scope provider handlers resolve against.
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Queries go straight to the process-wide engine with
    /// <paramref name="serviceProvider"/> as the handler-resolution scope, making the
    /// facade the only object built per resolution.
    /// </summary>
    /// <param name="engine">The singleton dispatch engine.</param>
    /// <param name="serviceProvider">The provider of the scope this facade serves.</param>
    public QueryMediator(
        MessageDispatchEngine engine,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _engine = engine;
        _serviceProvider = serviceProvider;
    }

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
        return _engine.DispatchAsync<TResult>(
            query,
            _serviceProvider,
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
        // Streams run against the invoker-cached pipeline plan — no per-call mediator
        // resolution and no composition lookup.
        return QueryStreamInvokerCache.Get<TResult>(query.GetType()).Stream(
            query, queryMediationSettings, cancellationToken, _engine, _serviceProvider);
    }

    /// <summary>
    /// Executes a query under a canonical group filter — no settings object, and with a
    /// reused <see cref="GroupSet"/> the grouped executor lookup matches on a single
    /// reference check. An empty set routes to the group-less fast lane.
    /// </summary>
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, GroupSet groups,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groups);

        IEnumerable<string>? effectiveGroups = groups.Count == 0 ? null : groups;

        return _engine.DispatchAsync<TResult>(query, _serviceProvider, null, cancellationToken, effectiveGroups);
    }

    /// <summary>
    /// Streaming counterpart of
    /// <see cref="QueryAsync{TResult}(IQuery{TResult}, GroupSet, CancellationToken)"/>:
    /// the group filter flows into the invoker's plan slot directly, with no settings
    /// object on the way.
    /// </summary>
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query, GroupSet groups,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groups);

        IEnumerable<string>? effectiveGroups = groups.Count == 0 ? null : groups;

        return QueryStreamInvokerCache.Get<TResult>(query.GetType()).Stream(
            query, null, cancellationToken, _engine, _serviceProvider, effectiveGroups);
    }

    /// <summary>
    /// Executes a query under an externally owned execution context — the nested-dispatch
    /// path: a handler opens a scope on its own context and passes the child here. The
    /// caller owns the context's lifetime; cancellation flows from the context.
    /// </summary>
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, ErgosfareContext context,
        QueryMediationSettings? queryMediationSettings = null)
    {
        return _engine.DispatchAsync<TResult>(
            query,
            context,
            _serviceProvider,
            queryMediationSettings?.Filters.Groups);
    }
}
