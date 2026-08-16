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
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query, IEnumerable<string>? groups,
        CancellationToken cancellationToken);

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
        IEnumerable<string>? groups, CancellationToken cancellationToken);

    /// <summary>Executes a query through its default pipeline.</summary>
    ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query,
        CancellationToken cancellationToken = default)
        => QueryAsync(query, (IEnumerable<string>?)null, cancellationToken);


    /// <summary>
    ///     Executes under a canonical group filter. Define the set once, statically, and the
    ///     cached pipeline matches it on a single reference check; <see cref="GroupSet.Empty"/>
    ///     dispatches the default pipeline.
    /// </summary>
    ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query, GroupSet groups,
        CancellationToken cancellationToken = default)
        => QueryAsync(query, groups.Count == 0 ? null : (IEnumerable<string>?)groups, cancellationToken);

    /// <summary>Executes under a group filter given as a plain array.</summary>
    ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query, string[] groups,
        CancellationToken cancellationToken = default)
        => QueryAsync(query, (IEnumerable<string>?)groups, cancellationToken);

    /// <summary>Streams under a caller-owned context, the way items reach and leave a stream.</summary>
    [Obsolete(StreamRevision.Notice)]
    IAsyncEnumerable<TQueryResult> StreamAsync<TQueryResult>(IStreamQuery<TQueryResult> query,
        ErgosfareContext context, IEnumerable<string>? groups = null);

    /// <summary>Streams a query through its default pipeline.</summary>
    [Obsolete(StreamRevision.Notice)]
    IAsyncEnumerable<TQueryResult> StreamAsync<TQueryResult>(IStreamQuery<TQueryResult> query,
        CancellationToken cancellationToken = default)
#pragma warning disable CS0618
        => StreamAsync(query, (IEnumerable<string>?)null, cancellationToken);
#pragma warning restore CS0618

    /// <summary>Streams under a canonical group filter.</summary>
    [Obsolete(StreamRevision.Notice)]
    IAsyncEnumerable<TQueryResult> StreamAsync<TQueryResult>(IStreamQuery<TQueryResult> query, GroupSet groups,
        CancellationToken cancellationToken = default)
#pragma warning disable CS0618
        => StreamAsync(query, groups.Count == 0 ? null : (IEnumerable<string>?)groups, cancellationToken);
#pragma warning restore CS0618

    /// <summary>Streams under a group filter given as a plain array.</summary>
    [Obsolete(StreamRevision.Notice)]
    IAsyncEnumerable<TQueryResult> StreamAsync<TQueryResult>(IStreamQuery<TQueryResult> query, string[] groups,
        CancellationToken cancellationToken = default)
#pragma warning disable CS0618
        => StreamAsync(query, (IEnumerable<string>?)groups, cancellationToken);
#pragma warning restore CS0618

    /// <summary>
    ///     Executes a query whose own type is named alongside its result, so the dispatch
    ///     reaches its pipeline through a compile-time constant pair rather than reading the
    ///     query's type back at run time.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Every other lane already takes the message as its type argument; this one could
    ///         not, because <typeparamref name="TQueryResult"/> has to be a type parameter for
    ///         the return type and C# does not infer type arguments through constraints. Naming
    ///         both is the price, and it is why these are additions rather than replacements:
    ///         <c>QueryAsync&lt;TQueryResult&gt;(IQuery&lt;TQueryResult&gt;)</c> stays the terse
    ///         form, and executing a query read off a queue is a legitimate shape whose
    ///         concrete type genuinely is a run-time fact.
    ///     </para>
    ///     <para>
    ///         Default implementations over the untyped calls, so an existing implementation
    ///         keeps compiling and simply forwards. What is gained is gained by overriding
    ///         them — <c>QueryMediator</c> does.
    ///     </para>
    ///     <para>
    ///         The streaming members are deliberately left untyped: their shape is under
    ///         revision, and adding a surface to something scheduled to change is work that
    ///         has to be undone.
    ///     </para>
    /// </remarks>
    /// <typeparam name="TQuery">The query's own type.</typeparam>
    /// <typeparam name="TQueryResult">The result the query declares.</typeparam>
    ValueTask<TQueryResult> QueryAsync<TQuery, TQueryResult>(TQuery query, IEnumerable<string>? groups,
        CancellationToken cancellationToken)
        where TQuery : IQuery<TQueryResult>
        => QueryAsync<TQueryResult>(query, groups, cancellationToken);

    /// <summary>Typed counterpart of the context query.</summary>
    ValueTask<TQueryResult> QueryAsync<TQuery, TQueryResult>(TQuery query, ErgosfareContext context,
        IEnumerable<string>? groups = null)
        where TQuery : IQuery<TQueryResult>
        => QueryAsync<TQueryResult>(query, context, groups);

    /// <summary>Typed query through the default pipeline.</summary>
    ValueTask<TQueryResult> QueryAsync<TQuery, TQueryResult>(TQuery query,
        CancellationToken cancellationToken = default)
        where TQuery : IQuery<TQueryResult>
        => QueryAsync<TQuery, TQueryResult>(query, (IEnumerable<string>?)null, cancellationToken);

    /// <summary>Typed counterpart of the canonical group-filter query.</summary>
    ValueTask<TQueryResult> QueryAsync<TQuery, TQueryResult>(TQuery query, GroupSet groups,
        CancellationToken cancellationToken = default)
        where TQuery : IQuery<TQueryResult>
        => QueryAsync<TQuery, TQueryResult>(query, groups.Count == 0 ? null : (IEnumerable<string>?)groups,
            cancellationToken);

    /// <summary>Typed counterpart of the array group-filter query.</summary>
    ValueTask<TQueryResult> QueryAsync<TQuery, TQueryResult>(TQuery query, string[] groups,
        CancellationToken cancellationToken = default)
        where TQuery : IQuery<TQueryResult>
        => QueryAsync<TQuery, TQueryResult>(query, (IEnumerable<string>?)groups, cancellationToken);
}
