using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;


/// <summary>
/// Represents a non-generic post-interceptor for queries in the Stella.Ergosfare pipeline.
/// </summary>
/// <remarks>
/// Post-interceptors run after the main query handler has executed. They can:
/// <list type="bullet">
/// <item>Inspect or modify the result of the query.</item>
/// <item>Perform logging, metrics collection, or additional side-effects.</item>
/// <item>Support asynchronous operations via <see cref="IAsyncPostInterceptor{TQuery}"/>.</item>
/// </list>
///
/// Use this interface when you do not need a strongly-typed modified result,
/// and the interceptor should work with any <see cref="IQuery"/>.
/// </remarks>
public interface IQueryPostInterceptor<in TQuery>: IQuery, IAsyncPostInterceptor<IQuery> where TQuery : IQuery;