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
public class QueryMediator : IQueryMediator
{
    /// <summary>
    /// Resolve strategy handed to the streaming invoker; identical on both construction
    /// shapes.
    /// </summary>
    private readonly ActualTypeOrFirstAssignableTypeMessageResolveStrategy _messageResolveStrategy;

    /// <summary>
    /// The mediator backing the original construction shape; null when the facade is
    /// engine-backed.
    /// </summary>
    private readonly IMessageMediator? _messageMediator;

    /// <summary>
    /// The singleton dispatch engine; null when the facade wraps an
    /// <see cref="IMessageMediator"/>.
    /// </summary>
    private readonly MessageDispatchEngine? _engine;

    /// <summary>
    /// The scope provider handlers resolve against on the engine path; the streaming path
    /// also resolves its <see cref="IMessageMediator"/> from it on demand.
    /// </summary>
    private readonly IServiceProvider? _serviceProvider;

    /// <summary>
    /// Wraps an existing <see cref="IMessageMediator"/> — the original construction shape,
    /// kept for direct construction and foreign mediator implementations.
    /// </summary>
    public QueryMediator(
        ActualTypeOrFirstAssignableTypeMessageResolveStrategy messageResolveStrategy,
        IMessageMediator messageMediator)
    {
        _messageResolveStrategy = messageResolveStrategy;
        _messageMediator = messageMediator;
    }

    /// <summary>
    /// Engine-backed construction: queries go straight to the process-wide engine with
    /// <paramref name="serviceProvider"/> as the handler-resolution scope, making the
    /// facade the only object built per resolution.
    /// </summary>
    /// <param name="engine">The singleton dispatch engine.</param>
    /// <param name="serviceProvider">The provider of the scope this facade serves.</param>
    /// <param name="messageResolveStrategy">Resolve strategy used by the streaming path.</param>
    public QueryMediator(
        MessageDispatchEngine engine,
        IServiceProvider serviceProvider,
        ActualTypeOrFirstAssignableTypeMessageResolveStrategy messageResolveStrategy)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _engine = engine;
        _serviceProvider = serviceProvider;
        _messageResolveStrategy = messageResolveStrategy;
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
        return _engine is not null
            ? _engine.DispatchAsync<TResult>(
                query,
                _serviceProvider!,
                queryMediationSettings?.Items,
                cancellationToken,
                queryMediationSettings?.Filters.Groups)
            : _messageMediator!.DispatchAsync<TResult>(
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
        // Engine-backed facades stream against the invoker-cached pipeline plan — no
        // per-call mediator resolution, MediateOptions or descriptor lookup; the wrapped
        // shape keeps the original Mediate path for foreign mediator implementations.
        return _engine is not null
            ? QueryStreamInvokerCache.Get<TResult>(query.GetType()).Stream(
                query, queryMediationSettings, cancellationToken, _engine, _serviceProvider!, _messageResolveStrategy)
            : QueryStreamInvokerCache.Get<TResult>(query.GetType()).Stream(
                query, queryMediationSettings, cancellationToken, RequireMessageMediator(), _messageResolveStrategy);
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

        return _engine is not null
            ? _engine.DispatchAsync<TResult>(query, _serviceProvider!, null, cancellationToken, effectiveGroups)
            : _messageMediator!.DispatchAsync<TResult>(query, null, cancellationToken, effectiveGroups);
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

        return _engine is not null
            ? QueryStreamInvokerCache.Get<TResult>(query.GetType()).Stream(
                query, null, cancellationToken, _engine, _serviceProvider!, _messageResolveStrategy, effectiveGroups)
            : QueryStreamInvokerCache.Get<TResult>(query.GetType()).Stream(
                query, null, cancellationToken, RequireMessageMediator(), _messageResolveStrategy, effectiveGroups);
    }

    /// <summary>
    /// Executes a query under an externally owned execution context — the nested-dispatch
    /// path: a handler opens a scope on its own context and passes the child here. The
    /// caller owns the context's lifetime; cancellation flows from the context.
    /// </summary>
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, IExecutionContext context,
        QueryMediationSettings? queryMediationSettings = null)
    {
        return _engine is not null
            ? _engine.DispatchAsync<TResult>(
                query,
                context,
                _serviceProvider!,
                queryMediationSettings?.Filters.Groups)
            : _messageMediator!.DispatchAsync<TResult>(
                query,
                context,
                queryMediationSettings?.Filters.Groups);
    }

    /// <summary>
    /// The mediator the wrapped-shape streaming path (which mediates through
    /// <c>Mediate(options)</c>) runs against — the wrapped instance, or the scope's own
    /// registration resolved on demand. Engine-backed facades never call this: their
    /// streams run the invoker's engine fast lane.
    /// </summary>
    private IMessageMediator RequireMessageMediator()
        => _messageMediator
           ?? (IMessageMediator?)_serviceProvider!.GetService(typeof(IMessageMediator))
           ?? throw new InvalidOperationException(
               "Streaming dispatch resolves IMessageMediator from the scope; register Ergosfare through AddErgosfare.");
}
