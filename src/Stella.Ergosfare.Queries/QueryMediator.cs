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

    /// <inheritdoc />
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, IEnumerable<string>? groups,
        CancellationToken cancellationToken)
        => _engine.DispatchAsync<TResult>(query, _serviceProvider, cancellationToken, groups);

    /// <inheritdoc />
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, ErgosfareContext context,
        IEnumerable<string>? groups = null)
        => _engine.DispatchAsync<TResult>(query, context, _serviceProvider, groups);

    /// <inheritdoc />
    [Obsolete(StreamRevision.Notice)]
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query, IEnumerable<string>? groups,
        CancellationToken cancellationToken)
        // Streams run against this container's cached pipeline — no per-call mediator
        // resolution and no composition lookup.
        => _engine.StreamAsync<TResult>(query, _serviceProvider, cancellationToken, groups);

    /// <inheritdoc />
    [Obsolete(StreamRevision.Notice)]
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query, ErgosfareContext context,
        IEnumerable<string>? groups = null)
        => _engine.StreamAsync<TResult>(query, context, _serviceProvider, groups);

    /// <summary>Executes a query through its default pipeline.</summary>
    /// <remarks>
    /// The conveniences are declared here as well as on the interface. They used to be
    /// extension methods, which a concrete-typed receiver finds; a default interface method is
    /// not, so carrying them only on the interface would have broken every call made through
    /// this class.
    /// </remarks>
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
        => QueryAsync(query, (IEnumerable<string>?)null, cancellationToken);


    /// <summary>Executes under a canonical group filter; an empty set routes to the group-less lane.</summary>
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, GroupSet groups,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groups);

        return QueryAsync(query, groups.Count == 0 ? null : (IEnumerable<string>?)groups, cancellationToken);
    }

    /// <summary>Executes under a group filter given as a plain array.</summary>
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, string[] groups,
        CancellationToken cancellationToken = default)
        => QueryAsync(query, (IEnumerable<string>?)groups, cancellationToken);

    /// <summary>Streams a query through its default pipeline.</summary>
    [Obsolete(StreamRevision.Notice)]
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query,
        CancellationToken cancellationToken = default)
        => StreamAsync(query, (IEnumerable<string>?)null, cancellationToken);

    /// <summary>Streams under a canonical group filter.</summary>
    [Obsolete(StreamRevision.Notice)]
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query, GroupSet groups,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groups);

        return StreamAsync(query, groups.Count == 0 ? null : (IEnumerable<string>?)groups, cancellationToken);
    }

    /// <summary>Streams under a group filter given as a plain array.</summary>
    [Obsolete(StreamRevision.Notice)]
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query, string[] groups,
        CancellationToken cancellationToken = default)
        => StreamAsync(query, (IEnumerable<string>?)groups, cancellationToken);

    /// <summary>
    /// Typed query: both the query's own type and its result reach the engine as type
    /// arguments, so the executor is a static generic field read instead of a lookup keyed
    /// by the query's run-time type.
    /// </summary>
    /// <inheritdoc cref="IQueryMediator.QueryAsync{TQuery,TQueryResult}(TQuery, IEnumerable{string}, CancellationToken)" path="/remarks"/>
    public ValueTask<TResult> QueryAsync<TQuery, TResult>(TQuery query, IEnumerable<string>? groups,
        CancellationToken cancellationToken)
        where TQuery : IQuery<TResult>
        => _engine.DispatchAsync<TQuery, TResult>(query, _serviceProvider, cancellationToken, groups);

    /// <summary>Typed counterpart of the context query.</summary>
    public ValueTask<TResult> QueryAsync<TQuery, TResult>(TQuery query, ErgosfareContext context,
        IEnumerable<string>? groups = null)
        where TQuery : IQuery<TResult>
        => _engine.DispatchAsync<TQuery, TResult>(query, context, _serviceProvider, groups);

    /// <summary>Typed query through the default pipeline.</summary>
    public ValueTask<TResult> QueryAsync<TQuery, TResult>(TQuery query, CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResult>
        => QueryAsync<TQuery, TResult>(query, (IEnumerable<string>?)null, cancellationToken);

    /// <summary>Typed counterpart of the canonical group-filter query.</summary>
    public ValueTask<TResult> QueryAsync<TQuery, TResult>(TQuery query, GroupSet groups,
        CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResult>
    {
        ArgumentNullException.ThrowIfNull(groups);

        return QueryAsync<TQuery, TResult>(query, groups.Count == 0 ? null : (IEnumerable<string>?)groups,
            cancellationToken);
    }

    /// <summary>Typed counterpart of the array group-filter query.</summary>
    public ValueTask<TResult> QueryAsync<TQuery, TResult>(TQuery query, string[] groups,
        CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResult>
        => QueryAsync<TQuery, TResult>(query, (IEnumerable<string>?)groups, cancellationToken);
}
