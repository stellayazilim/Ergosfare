using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Queries.Abstractions;


/// <summary>
/// Represents a type-safe pre-interceptor for queries in the Ergosfare pipeline.
/// </summary>
/// <typeparam name="TQuery">
/// The type of query to intercept. Must implement <see cref="IQuery"/>.
/// </typeparam>
/// <typeparam name="TModifiedQuery">
/// The type of query that may be returned after interception. Must derive from <typeparamref name="TQuery"/>.
/// </typeparam>
/// <remarks>
/// Pre-interceptors run before the main query handler is invoked. They can:
/// <list type="bullet">
/// <item>Modify the incoming query and return a new instance of <typeparamref name="TModifiedQuery"/>.</item>
/// <item>Perform validation, logging, or enrichment of the query.</item>
/// <item>Support asynchronous operations via <see cref="IAsyncPreInterceptor{TQuery}"/>.</item>
/// </list>
/// 
/// Use this generic version if you want strong typing for the returned modified query,
/// avoiding casts from <see cref="object"/>.
/// </remarks>
public interface IQueryPreInterceptor<in TQuery, TModifiedQuery>
    :IQuery, IAsyncPreInterceptor<TQuery> where TQuery: IQuery
    where TModifiedQuery: TQuery 
{
    
    /// <inheritdoc cref="IAsyncPreInterceptor{TQuery}.HandleAsync(TQuery,IExecutionContext)"/>
    async Task<object> IAsyncPreInterceptor<TQuery>.HandleAsync(TQuery query, IExecutionContext executionContext)
    {
        return (await HandleAsync(query, executionContext))!;
    }

    /// <summary>
    /// Intercepts the specified query before it reaches the main handler.
    /// </summary>
    /// <param name="query">The incoming query to intercept.</param>
    /// <param name="executionContext">The current execution context for the pipeline.</param>
    /// <returns>
    /// A task returning a modified query of type <typeparamref name="TModifiedQuery"/>.
    /// May return <c>null</c> if no modification is made.
    /// </returns>
    new Task<TModifiedQuery?> HandleAsync(TQuery query, IExecutionContext executionContext);
}