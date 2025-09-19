using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Queries.Abstractions;


/// <summary>
/// Represents a type-safe final interceptor for queries, allowing custom logic
/// to execute after all query handlers and other interceptors have completed.
/// </summary>
/// <typeparam name="TQuery">The type of query being intercepted. Must implement
/// <see cref="IQuery{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The result type returned by the query.</typeparam>
/// <remarks>
/// <para>
/// Implementing this interface allows final processing logic to run after the query
/// has been handled by all handlers and interceptors in the mediation pipeline.
/// </para>
/// <para>
/// This interface inherits from <see cref="IAsyncFinalInterceptor{TQuery, TResult}"/>,
/// enabling asynchronous post-processing of query results.
/// </para>
/// </remarks>
public interface IQueryFinalInterceptor<in TQuery, in TResult>: IQuery, IAsyncFinalInterceptor<TQuery, TResult> 
    where TQuery : IQuery<TResult>;