using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
///     Represents the mediator interface for executing queries within the application.
/// </summary>
/// <remarks>
///     Everything a dispatch can be told is a parameter. A settings object used to carry the
///     same two things, and carrying them that way meant allocating one per dispatch and
///     reading it at dispatch time — a shape nothing can be compiled from. The conveniences
///     below are default implementations over the full calls, so an implementation writes
///     four methods and inherits the rest.
/// </remarks>
public interface IQueryMediator : IMessage
{
    /// <summary>
    ///     Executes a query and returns its result.
    /// </summary>
    /// <typeparam name="TQueryResult">The type of the result returned by the query.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="groups">
    ///     The group filter, or <c>null</c> for the default pipeline. A reused
    ///     <see cref="GroupSet"/> matches the cached pipeline on a single reference check.
    /// </param>
    /// <param name="items">
    ///     Contextual items exposed to the pipeline. The dictionary is the caller's, and
    ///     participants writing into it is how a dispatch hands anything back besides its result.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query, IEnumerable<string>? groups,
        IDictionary<object, object?>? items, CancellationToken cancellationToken);

    /// <summary>
    ///     Executes under an externally owned execution context — the nested-dispatch path: a
    ///     handler opens a scope on its own context (<c>using var scope = context.CreateScope();</c>)
    ///     and passes <c>scope.Context</c> here. The caller owns the context's lifetime;
    ///     cancellation flows from the context.
    /// </summary>
    ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query, ErgosfareContext context,
        IEnumerable<string>? groups = null);

    /// <summary>
    ///     Streams the results of a query.
    /// </summary>
    /// <remarks>
    ///     The sequence is produced by the handler and enumerated by the caller, so the pipeline
    ///     runs as the caller pulls rather than before this call returns.
    /// </remarks>
    [Obsolete(StreamRevision.Notice)]
    IAsyncEnumerable<TQueryResult> StreamAsync<TQueryResult>(IStreamQuery<TQueryResult> query,
        IEnumerable<string>? groups, IDictionary<object, object?>? items, CancellationToken cancellationToken);

    /// <summary>Executes a query through its default pipeline.</summary>
    ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query,
        CancellationToken cancellationToken = default)
        => QueryAsync(query, null, null, cancellationToken);

    /// <summary>Executes with contextual items the pipeline can read and write.</summary>
    ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query,
        IDictionary<object, object?> items, CancellationToken cancellationToken = default)
        => QueryAsync(query, null, items, cancellationToken);

    /// <summary>
    ///     Executes under a canonical group filter. Define the set once, statically, and the
    ///     cached pipeline matches it on a single reference check; <see cref="GroupSet.Empty"/>
    ///     dispatches the default pipeline.
    /// </summary>
    ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query, GroupSet groups,
        CancellationToken cancellationToken = default)
        => QueryAsync(query, groups.Count == 0 ? null : groups, null, cancellationToken);

    /// <summary>Executes under a group filter given as a plain array.</summary>
    ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query, string[] groups,
        CancellationToken cancellationToken = default)
        => QueryAsync(query, groups, null, cancellationToken);

    /// <summary>Streams a query through its default pipeline.</summary>
    [Obsolete(StreamRevision.Notice)]
    IAsyncEnumerable<TQueryResult> StreamAsync<TQueryResult>(IStreamQuery<TQueryResult> query,
        CancellationToken cancellationToken = default)
#pragma warning disable CS0618
        => StreamAsync(query, null, null, cancellationToken);
#pragma warning restore CS0618

    /// <summary>Streams under a canonical group filter.</summary>
    [Obsolete(StreamRevision.Notice)]
    IAsyncEnumerable<TQueryResult> StreamAsync<TQueryResult>(IStreamQuery<TQueryResult> query, GroupSet groups,
        CancellationToken cancellationToken = default)
#pragma warning disable CS0618
        => StreamAsync(query, groups.Count == 0 ? null : groups, null, cancellationToken);
#pragma warning restore CS0618

    /// <summary>Streams under a group filter given as a plain array.</summary>
    [Obsolete(StreamRevision.Notice)]
    IAsyncEnumerable<TQueryResult> StreamAsync<TQueryResult>(IStreamQuery<TQueryResult> query, string[] groups,
        CancellationToken cancellationToken = default)
#pragma warning disable CS0618
        => StreamAsync(query, groups, null, cancellationToken);
#pragma warning restore CS0618
}
