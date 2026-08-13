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
        IDictionary<object, object?>? items, CancellationToken cancellationToken)
        => _engine.DispatchAsync<TResult>(query, _serviceProvider, items, cancellationToken, groups);

    /// <inheritdoc />
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, ErgosfareContext context,
        IEnumerable<string>? groups = null)
        => _engine.DispatchAsync<TResult>(query, context, _serviceProvider, groups);

    /// <inheritdoc />
    [Obsolete(StreamRevision.Notice)]
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query, IEnumerable<string>? groups,
        IDictionary<object, object?>? items, CancellationToken cancellationToken)
        // Streams run against this container's cached pipeline — no per-call mediator
        // resolution and no composition lookup.
        => _engine.StreamAsync<TResult>(query, _serviceProvider, items, cancellationToken, groups);

    /// <summary>Executes a query through its default pipeline.</summary>
    /// <remarks>
    /// The conveniences are declared here as well as on the interface. They used to be
    /// extension methods, which a concrete-typed receiver finds; a default interface method is
    /// not, so carrying them only on the interface would have broken every call made through
    /// this class.
    /// </remarks>
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
        => QueryAsync(query, null, null, cancellationToken);

    /// <summary>Executes with contextual items the pipeline can read and write.</summary>
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, IDictionary<object, object?> items,
        CancellationToken cancellationToken = default)
        => QueryAsync(query, null, items, cancellationToken);

    /// <summary>Executes under a canonical group filter; an empty set routes to the group-less lane.</summary>
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, GroupSet groups,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groups);

        return QueryAsync(query, groups.Count == 0 ? null : groups, null, cancellationToken);
    }

    /// <summary>Executes under a group filter given as a plain array.</summary>
    public ValueTask<TResult> QueryAsync<TResult>(IQuery<TResult> query, string[] groups,
        CancellationToken cancellationToken = default)
        => QueryAsync(query, groups, null, cancellationToken);

    /// <summary>Streams a query through its default pipeline.</summary>
    [Obsolete(StreamRevision.Notice)]
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query,
        CancellationToken cancellationToken = default)
        => StreamAsync(query, null, null, cancellationToken);

    /// <summary>Streams under a canonical group filter.</summary>
    [Obsolete(StreamRevision.Notice)]
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query, GroupSet groups,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groups);

        return StreamAsync(query, groups.Count == 0 ? null : groups, null, cancellationToken);
    }

    /// <summary>Streams under a group filter given as a plain array.</summary>
    [Obsolete(StreamRevision.Notice)]
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query, string[] groups,
        CancellationToken cancellationToken = default)
        => StreamAsync(query, groups, null, cancellationToken);
}
