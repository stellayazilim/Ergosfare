using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Queries.Abstractions;

/// <summary>
/// Represents a type-safe asynchronous handler for a query of type <typeparamref name="TQuery"/>,
/// producing a result of type <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TQuery">The type of query being handled. Must implement <see cref="IQuery{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The type of result returned by the query.</typeparam>
/// <remarks>
/// <para>
/// Implementing this interface allows a handler to process a query asynchronously
/// within the query mediation pipeline and return a strongly-typed result.
/// </para>
/// <para>
/// Handlers implementing this interface are automatically recognized and invoked
/// by the query mediator when the corresponding query type is dispatched.
/// </para>
/// </remarks>
public interface IQueryHandler<in TQuery,TResult>: IQuery, IAsyncHandler<TQuery, TResult> 
    where TQuery : IQuery<TResult>;