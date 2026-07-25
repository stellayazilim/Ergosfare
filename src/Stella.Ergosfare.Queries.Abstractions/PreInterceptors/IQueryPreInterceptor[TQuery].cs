using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// Represents a type-safe pre-interceptor for query messages. It runs before the query
/// handler and returns the query that continues through the pipeline — the original, or a
/// rewritten one.
/// </summary>
/// <typeparam name="TQuery">The type of query to be intercepted. Must implement <see cref="IQuery"/>.</typeparam>
/// <remarks>
/// A pre-interceptor carries no result, so the single-parameter form returns the query type
/// directly rather than <see cref="object"/>. Use the non-generic
/// <see cref="IQueryPreInterceptor"/> to intercept any query, or
/// <see cref="IQueryPreInterceptor{TQuery, TModifiedQuery}"/> to return a different, derived
/// query type. <typeparamref name="TQuery"/> is invariant because it is returned.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IQueryPreInterceptor<TQuery> : IQuery, IAsyncPreInterceptor<TQuery>
    where TQuery : IQuery
{
    /// <inheritdoc cref="IAsyncPreInterceptor{TMessage}.HandleAsync(TMessage,IExecutionContext)"/>
    async ValueTask<object> IAsyncPreInterceptor<TQuery>.HandleAsync(TQuery query, IExecutionContext context)
        => await HandleAsync(query, context);

    /// <summary>
    /// Handles the query before its handler runs and returns the query that continues through
    /// the pipeline (the original, or a rewritten instance).
    /// </summary>
    /// <param name="query">The query to intercept.</param>
    /// <param name="context">The current execution context.</param>
    new ValueTask<TQuery> HandleAsync(TQuery query, IExecutionContext context);
}
