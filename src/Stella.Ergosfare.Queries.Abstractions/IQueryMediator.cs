using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// Executes queries against their handlers.
/// </summary>
/// <remarks>
/// Everything a dispatch needs is passed as an argument. Only the three overloads taking
/// <c>IEnumerable&lt;string&gt;</c> groups or an <see cref="ErgosfareContext"/> are
/// abstract; the rest are conveniences implemented in terms of those, so an implementation
/// writes those and inherits the others.
/// </remarks>
public interface IQueryMediator : IMessage
{
    /// <summary>
    /// Executes <paramref name="query"/> and returns its result.
    /// </summary>
    /// <typeparam name="TQueryResult">The result type the query declares.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="groups">
    /// The groups to run; <c>null</c> runs the default group. Reusing a
    /// <see cref="GroupSet"/> lets the cached pipeline be matched by reference.
    /// </param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query, IEnumerable<string>? groups,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes <paramref name="query"/> under an execution context supplied by the caller —
    /// the shape a nested query uses.
    /// </summary>
    /// <typeparam name="TQueryResult">The result type the query declares.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="context">
    /// The context to run under, typically a child opened with
    /// <c>using var scope = context.CreateScope();</c> and passed as <c>scope.Context</c>.
    /// The caller owns its lifetime, and cancellation comes from it.
    /// </param>
    /// <param name="groups">The groups to run; <c>null</c> runs the default group.</param>
    /// <returns>The result the handler produced.</returns>
    ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query, ErgosfareContext context,
        IEnumerable<string>? groups = null);

    /// <summary>
    /// Streams the results of <paramref name="query"/>.
    /// </summary>
    /// <typeparam name="TQueryResult">The type of each streamed item.</typeparam>
    /// <param name="query">The query to stream.</param>
    /// <param name="groups">The groups to run; <c>null</c> runs the default group.</param>
    /// <param name="cancellationToken">Token for the enumeration.</param>
    /// <returns>The streamed results.</returns>
    /// <remarks>
    /// The handler produces items as the caller enumerates them, so the pipeline runs while
    /// the caller pulls rather than before this method returns.
    /// </remarks>
    [Obsolete(StreamRevision.Notice)]
    IAsyncEnumerable<TQueryResult> StreamAsync<TQueryResult>(IStreamQuery<TQueryResult> query,
        IEnumerable<string>? groups, CancellationToken cancellationToken);

    /// <summary>
    /// Executes <paramref name="query"/> through its default pipeline.
    /// </summary>
    /// <typeparam name="TQueryResult">The result type the query declares.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query,
        CancellationToken cancellationToken = default)
        => QueryAsync(query, (IEnumerable<string>?)null, cancellationToken);


    /// <summary>
    /// Executes <paramref name="query"/> under a canonical group set.
    /// </summary>
    /// <typeparam name="TQueryResult">The result type the query declares.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="groups">
    /// The groups to run. Build the set once and reuse it, and the cached pipeline is
    /// matched by reference; <see cref="GroupSet.Empty"/> runs the default pipeline.
    /// </param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query, GroupSet groups,
        CancellationToken cancellationToken = default)
        => QueryAsync(query, groups.Count == 0 ? null : (IEnumerable<string>?)groups, cancellationToken);

    /// <summary>
    /// Executes <paramref name="query"/> under groups given as an array.
    /// </summary>
    /// <typeparam name="TQueryResult">The result type the query declares.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="groups">The groups to run.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query, string[] groups,
        CancellationToken cancellationToken = default)
        => QueryAsync(query, (IEnumerable<string>?)groups, cancellationToken);

    /// <summary>
    /// Streams the results of <paramref name="query"/> under a caller-owned execution
    /// context.
    /// </summary>
    /// <typeparam name="TQueryResult">The type of each streamed item.</typeparam>
    /// <param name="query">The query to stream.</param>
    /// <param name="context">The context to run under; the caller owns its lifetime.</param>
    /// <param name="groups">The groups to run; <c>null</c> runs the default group.</param>
    /// <returns>The streamed results.</returns>
    [Obsolete(StreamRevision.Notice)]
    IAsyncEnumerable<TQueryResult> StreamAsync<TQueryResult>(IStreamQuery<TQueryResult> query,
        ErgosfareContext context, IEnumerable<string>? groups = null);

    /// <summary>
    /// Streams the results of <paramref name="query"/> through its default pipeline.
    /// </summary>
    /// <typeparam name="TQueryResult">The type of each streamed item.</typeparam>
    /// <param name="query">The query to stream.</param>
    /// <param name="cancellationToken">Token for the enumeration.</param>
    /// <returns>The streamed results.</returns>
    [Obsolete(StreamRevision.Notice)]
    IAsyncEnumerable<TQueryResult> StreamAsync<TQueryResult>(IStreamQuery<TQueryResult> query,
        CancellationToken cancellationToken = default)
#pragma warning disable CS0618
        => StreamAsync(query, (IEnumerable<string>?)null, cancellationToken);
#pragma warning restore CS0618

    /// <summary>
    /// Streams the results of <paramref name="query"/> under a canonical group set.
    /// </summary>
    /// <typeparam name="TQueryResult">The type of each streamed item.</typeparam>
    /// <param name="query">The query to stream.</param>
    /// <param name="groups">The groups to run; <see cref="GroupSet.Empty"/> runs the default pipeline.</param>
    /// <param name="cancellationToken">Token for the enumeration.</param>
    /// <returns>The streamed results.</returns>
    [Obsolete(StreamRevision.Notice)]
    IAsyncEnumerable<TQueryResult> StreamAsync<TQueryResult>(IStreamQuery<TQueryResult> query, GroupSet groups,
        CancellationToken cancellationToken = default)
#pragma warning disable CS0618
        => StreamAsync(query, groups.Count == 0 ? null : (IEnumerable<string>?)groups, cancellationToken);
#pragma warning restore CS0618

    /// <summary>
    /// Streams the results of <paramref name="query"/> under groups given as an array.
    /// </summary>
    /// <typeparam name="TQueryResult">The type of each streamed item.</typeparam>
    /// <param name="query">The query to stream.</param>
    /// <param name="groups">The groups to run.</param>
    /// <param name="cancellationToken">Token for the enumeration.</param>
    /// <returns>The streamed results.</returns>
    [Obsolete(StreamRevision.Notice)]
    IAsyncEnumerable<TQueryResult> StreamAsync<TQueryResult>(IStreamQuery<TQueryResult> query, string[] groups,
        CancellationToken cancellationToken = default)
#pragma warning disable CS0618
        => StreamAsync(query, (IEnumerable<string>?)groups, cancellationToken);
#pragma warning restore CS0618

    /// <summary>
    /// Executes <paramref name="query"/> naming its own type alongside its result, so the
    /// pipeline is reached through a pair of compile-time constants instead of the query's
    /// type being read back at run time.
    /// </summary>
    /// <typeparam name="TQuery">The query's own type.</typeparam>
    /// <typeparam name="TQueryResult">The result type the query declares.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="groups">The groups to run; <c>null</c> runs the default group.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    /// <remarks>
    /// <para>
    /// Both type arguments have to be named: <typeparamref name="TQueryResult"/> must be a
    /// type parameter for the return type, and C# will not infer type arguments through a
    /// constraint. That is why these overloads are additions rather than replacements —
    /// <c>QueryAsync&lt;TQueryResult&gt;(IQuery&lt;TQueryResult&gt;)</c> stays the short
    /// form, and a query read off a queue genuinely does not know its type until run time.
    /// </para>
    /// <para>
    /// The default implementation simply forwards to the untyped call, so an existing
    /// implementation keeps working; the benefit comes from overriding it, as
    /// <c>QueryMediator</c> does.
    /// </para>
    /// <para>
    /// The streaming members have no typed counterpart on purpose: their shape is being
    /// reworked, and adding surface to something already scheduled to change would only have
    /// to be undone.
    /// </para>
    /// </remarks>
    ValueTask<TQueryResult> QueryAsync<TQuery, TQueryResult>(TQuery query, IEnumerable<string>? groups,
        CancellationToken cancellationToken)
        where TQuery : IQuery<TQueryResult>
        => QueryAsync<TQueryResult>(query, groups, cancellationToken);

    /// <summary>
    /// Executes <paramref name="query"/> under a caller-owned context, naming both types.
    /// </summary>
    /// <typeparam name="TQuery">The query's own type.</typeparam>
    /// <typeparam name="TQueryResult">The result type the query declares.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="context">The context to run under; the caller owns its lifetime.</param>
    /// <param name="groups">The groups to run; <c>null</c> runs the default group.</param>
    /// <returns>The result the handler produced.</returns>
    ValueTask<TQueryResult> QueryAsync<TQuery, TQueryResult>(TQuery query, ErgosfareContext context,
        IEnumerable<string>? groups = null)
        where TQuery : IQuery<TQueryResult>
        => QueryAsync<TQueryResult>(query, context, groups);

    /// <summary>
    /// Executes <paramref name="query"/> through its default pipeline, naming both types.
    /// </summary>
    /// <typeparam name="TQuery">The query's own type.</typeparam>
    /// <typeparam name="TQueryResult">The result type the query declares.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    ValueTask<TQueryResult> QueryAsync<TQuery, TQueryResult>(TQuery query,
        CancellationToken cancellationToken = default)
        where TQuery : IQuery<TQueryResult>
        => QueryAsync<TQuery, TQueryResult>(query, (IEnumerable<string>?)null, cancellationToken);

    /// <summary>
    /// Executes <paramref name="query"/> under a canonical group set, naming both types.
    /// </summary>
    /// <typeparam name="TQuery">The query's own type.</typeparam>
    /// <typeparam name="TQueryResult">The result type the query declares.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="groups">The groups to run; <see cref="GroupSet.Empty"/> runs the default pipeline.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    ValueTask<TQueryResult> QueryAsync<TQuery, TQueryResult>(TQuery query, GroupSet groups,
        CancellationToken cancellationToken = default)
        where TQuery : IQuery<TQueryResult>
        => QueryAsync<TQuery, TQueryResult>(query, groups.Count == 0 ? null : (IEnumerable<string>?)groups,
            cancellationToken);

    /// <summary>
    /// Executes <paramref name="query"/> under groups given as an array, naming both types.
    /// </summary>
    /// <typeparam name="TQuery">The query's own type.</typeparam>
    /// <typeparam name="TQueryResult">The result type the query declares.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="groups">The groups to run.</param>
    /// <param name="cancellationToken">Token exposed on the execution context.</param>
    /// <returns>The result the handler produced.</returns>
    ValueTask<TQueryResult> QueryAsync<TQuery, TQueryResult>(TQuery query, string[] groups,
        CancellationToken cancellationToken = default)
        where TQuery : IQuery<TQueryResult>
        => QueryAsync<TQuery, TQueryResult>(query, (IEnumerable<string>?)groups, cancellationToken);
}
