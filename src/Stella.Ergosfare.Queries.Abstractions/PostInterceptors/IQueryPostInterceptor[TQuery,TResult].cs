using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;



/// <summary>
/// Represents a non-type-safe post-interceptor for queries in the Stella.Ergosfare pipeline.
/// </summary>
/// <typeparam name="TQuery">The type of query being intercepted.</typeparam>
/// <typeparam name="TResult">The produced result in pipeline</typeparam>
/// <remarks>
/// Post-interceptors execute after the main query handler has processed the query. 
/// They allow you to:
/// <list type="bullet">
/// <item>Inspect or modify the query result.</item>
/// <item>Perform logging, metrics collection, or additional asynchronous side-effects.</item>
/// <item>Work with the query result as an <see cref="object"/>, instead of a strongly-typed <typeparamref name="TResult"/>.</item>
/// </list>
///
/// This interface is suitable when type safety is not required or when using generic pipelines
/// that handle multiple query/result types uniformly.
/// </remarks>
public interface IQueryPostInterceptor<in TQuery, in TResult>: IQuery, IAsyncPostInterceptor<TQuery, TResult>
    where TQuery: IQuery<TResult> where TResult: notnull;