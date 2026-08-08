using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;


/// <summary>
/// Represents a result-agnostic post-interceptor for a specific query type in the
/// Stella.Ergosfare pipeline.
/// </summary>
/// <remarks>
/// Post-interceptors run after the main query handler has executed. They can:
/// <list type="bullet">
/// <item>Inspect or modify the result of the query.</item>
/// <item>Perform logging, metrics collection, or additional side-effects.</item>
/// <item>Support asynchronous operations via <see cref="IAsyncPostInterceptor{TQuery}"/>.</item>
/// </list>
///
/// Use this interface when you do not need a strongly-typed result but want the
/// interceptor scoped to <typeparamref name="TQuery"/>. The base previously closed over
/// <see cref="IQuery"/> instead of <typeparamref name="TQuery"/>, which registered the
/// interceptor for every query in the application rather than the targeted one. Use the
/// non-generic <see cref="IQueryPostInterceptor"/> to intercept all queries.
/// </remarks>
public interface IQueryPostInterceptor<in TQuery>: IQuery, IAsyncPostInterceptor<TQuery> where TQuery : IQuery;