using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Queries;


/// <summary>
/// The query mediator an application resolves: it holds the scope it was resolved from and
/// hands every query and stream to the container's dispatch engine.
/// </summary>
/// <remarks>
/// The engine is shared across the process and this facade is the only object built per
/// resolution.
/// </remarks>
public class QueryMediator : IQueryMediator
{
    /// <summary>
    /// The container's dispatch engine, shared by every scope.
    /// </summary>
    private readonly MessageDispatchEngine _engine;

    /// <summary>
    /// The provider of the scope this facade was resolved from; participants resolve
    /// against it.
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes the facade over a container's engine and the scope it serves.
    /// </summary>
    /// <param name="engine">The container's dispatch engine.</param>
    /// <param name="serviceProvider">The provider of the scope this facade serves.</param>
    /// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
    internal QueryMediator(
        MessageDispatchEngine engine,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _engine = engine;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, GroupSet groups,
        CancellationToken cancellationToken = default)
        => _engine.DispatchAsync<TResult>(query, _serviceProvider, cancellationToken, groups ?? throw new ArgumentNullException(nameof(groups)));

    /// <inheritdoc />
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, ErgosfareContext context,
        GroupSet? groups = null)
        => _engine.DispatchAsync<TResult>(query, context, _serviceProvider, groups);

    /// <inheritdoc />
    [Obsolete(StreamRevision.Notice)]
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query, GroupSet groups,
        CancellationToken cancellationToken = default)
        // The engine keeps this container's streaming pipelines, so nothing is resolved or
        // looked up per call.
        => _engine.StreamAsync<TResult>(query, _serviceProvider, cancellationToken, groups ?? throw new ArgumentNullException(nameof(groups)));

    /// <inheritdoc />
    [Obsolete(StreamRevision.Notice)]
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query, ErgosfareContext context,
        GroupSet? groups = null)
        => _engine.StreamAsync<TResult>(query, context, _serviceProvider, groups);

    /// <summary>
    /// Executes <paramref name="query"/> through its default pipeline.
    /// </summary>
    /// <typeparam name="TResult">The result type the query declares.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    /// <remarks>
    /// The conveniences are declared on this class as well as on the interface. A call made
    /// through the concrete type does not find a default interface method, so declaring them
    /// only on the interface would leave those calls without an overload to bind to.
    /// </remarks>
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
        => QueryAsync(query, GroupSet.Empty, cancellationToken);


    /// <summary>
    /// Streams the results of <paramref name="query"/> through its default pipeline.
    /// </summary>
    /// <typeparam name="TResult">The type of each streamed item.</typeparam>
    /// <param name="query">The query to stream.</param>
    /// <param name="cancellationToken">Token for the enumeration.</param>
    /// <returns>The streamed results.</returns>
    [Obsolete(StreamRevision.Notice)]
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query,
        CancellationToken cancellationToken = default)
        => StreamAsync(query, GroupSet.Empty, cancellationToken);

    /// <summary>
    /// Executes <paramref name="query"/> naming both its own type and its result, so the
    /// pipeline is found through a static generic field rather than a lookup on the query's
    /// runtime type.
    /// </summary>
    /// <typeparam name="TQuery">The query's own type.</typeparam>
    /// <typeparam name="TResult">The result type the query declares.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="groups">The groups to run; an empty set runs the default group.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    public ValueTask<TResult> QueryAsync<TQuery, TResult>(TQuery query, GroupSet groups,
        CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResult>
        => _engine.DispatchAsync<TQuery, TResult>(query, _serviceProvider, cancellationToken, groups ?? throw new ArgumentNullException(nameof(groups)));

    /// <summary>
    /// Executes <paramref name="query"/> under a caller-owned context, naming both types.
    /// </summary>
    /// <typeparam name="TQuery">The query's own type.</typeparam>
    /// <typeparam name="TResult">The result type the query declares.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="context">The context to run under; the caller owns its lifetime.</param>
    /// <param name="groups">The groups to run; an empty set runs the default group.</param>
    /// <returns>The result the handler produced.</returns>
    public ValueTask<TResult> QueryAsync<TQuery, TResult>(TQuery query, ErgosfareContext context,
        GroupSet? groups = null)
        where TQuery : IQuery<TResult>
        => _engine.DispatchAsync<TQuery, TResult>(query, context, _serviceProvider, groups);

    /// <summary>
    /// Executes <paramref name="query"/> through its default pipeline, naming both types.
    /// </summary>
    /// <typeparam name="TQuery">The query's own type.</typeparam>
    /// <typeparam name="TResult">The result type the query declares.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    public ValueTask<TResult> QueryAsync<TQuery, TResult>(TQuery query, CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResult>
        => QueryAsync<TQuery, TResult>(query, GroupSet.Empty, cancellationToken);

}
