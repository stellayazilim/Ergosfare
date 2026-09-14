using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// Handles queries of type <typeparamref name="TQuery"/> and returns the
/// <typeparamref name="TResult"/> they declare.
/// </summary>
/// <typeparam name="TQuery">The query type this handler accepts.</typeparam>
/// <typeparam name="TResult">The result type the query declares.</typeparam>
/// <remarks>
/// A query is served by exactly one handler, so registering two for the same query type
/// fails the dispatch. Registration finds this handler through the contract itself — there
/// is nothing to wire up by hand.
/// </remarks>
public interface IQueryHandler<in TQuery,TResult>: IQuery, IAsyncHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>;
