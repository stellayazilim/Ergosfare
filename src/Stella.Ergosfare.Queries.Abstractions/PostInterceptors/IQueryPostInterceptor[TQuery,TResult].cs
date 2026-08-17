using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// Runs after the handler of a <typeparamref name="TQuery"/> and decides what result the
/// caller receives.
/// </summary>
/// <typeparam name="TQuery">The query type this interceptor accepts.</typeparam>
/// <typeparam name="TResult">The result type the query declares.</typeparam>
/// <remarks>
/// <typeparamref name="TQuery"/> is contravariant, so an interceptor written against a base
/// query type also runs for the queries derived from it, while
/// <typeparamref name="TResult"/> stays invariant because it is returned.
/// </remarks>
public interface IQueryPostInterceptor<in TQuery, TResult> : IQuery, IAsyncPostInterceptor<TQuery, TResult>
    where TQuery : IQuery<TResult>
    where TResult : notnull
{
    /// <summary>
    /// Forwards the core contract to the typed method below.
    /// </summary>
    /// <param name="query">The query that was handled.</param>
    /// <param name="messageResult">The result as the previous stage left it.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>The result the typed method returned.</returns>
    async ValueTask<object> IAsyncPostInterceptor<TQuery, TResult>.HandleAsync(
        TQuery query, TResult messageResult, ErgosfareContext context)
        => await HandleAsync(query, messageResult, context);

    /// <summary>
    /// Processes the result of handling <paramref name="query"/>.
    /// </summary>
    /// <param name="query">The query that was handled.</param>
    /// <param name="queryResult">The result as the previous stage left it.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>
    /// The result the rest of the pipeline receives — either the one passed in or a
    /// replacement.
    /// </returns>
    new ValueTask<TResult> HandleAsync(TQuery query, TResult queryResult, ErgosfareContext context);
}
