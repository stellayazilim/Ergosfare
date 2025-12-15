using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;


/// <summary>
/// Represents an exception interceptor for queries with a specific result type.
/// </summary>
/// <typeparam name="TQuery">The type of query this interceptor handles. Must implement <see cref="IQuery{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The result type of the query. Must be non-nullable.</typeparam>
/// <remarks>
/// This is a non-type-safe version in the sense that the interceptor returns <see cref="object"/> 
/// when invoked through <see cref="IAsyncExceptionInterceptor{TQuery,TResult}"/>, 
/// but it still enforces the <typeparamref name="TQuery"/> and <typeparamref name="TResult"/> constraints.
///
/// Use this interface when you want to register an exception interceptor for a specific query type
/// and result type, but do not need a strongly typed modified result.
/// 
/// For scenarios requiring a type-safe modified result, consider using
/// <see cref="IQueryExceptionInterceptor{TQuery,TResult,TModifiedResult}"/>.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IQueryExceptionInterceptor<in TQuery, in TResult>
    : IQuery,IAsyncExceptionInterceptor<TQuery, TResult>
    where TQuery : IQuery<TResult>
    where TResult : notnull;