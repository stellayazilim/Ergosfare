using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// Runs before the handler of a <typeparamref name="TQuery"/> and decides which query the
/// rest of the pipeline sees.
/// </summary>
/// <typeparam name="TQuery">The query type this interceptor accepts.</typeparam>
/// <remarks>
/// A pre-interceptor produces no result, so this form returns the query type itself rather
/// than <see cref="object"/> — which is why <typeparamref name="TQuery"/> is invariant here.
/// Returning a derived query is allowed and needs nothing extra: it is still a
/// <typeparamref name="TQuery"/>. Use <see cref="IQueryPreInterceptor"/> to accept any query
/// instead.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IQueryPreInterceptor<TQuery> : IQuery, IAsyncPreInterceptor<TQuery>
    where TQuery : IQuery
{
    /// <summary>
    /// Forwards the core contract to the typed method below.
    /// </summary>
    /// <param name="query">The query as the previous stage left it.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>The query the typed method returned.</returns>
    async ValueTask<object> IAsyncPreInterceptor<TQuery>.HandleAsync(TQuery query, ErgosfareContext context)
        => await HandleAsync(query, context);

    /// <summary>
    /// Processes <paramref name="query"/> before its handler runs.
    /// </summary>
    /// <param name="query">The query as the previous stage left it.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>
    /// The query the rest of the pipeline receives — either the one passed in or a
    /// replacement.
    /// </returns>
    new ValueTask<TQuery> HandleAsync(TQuery query, ErgosfareContext context);
}
