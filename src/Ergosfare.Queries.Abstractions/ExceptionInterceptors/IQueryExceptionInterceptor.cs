using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Queries.Abstractions;


/// <summary>
/// Represents a query exception interceptor that can handle multiple query types in a non-generic, non-type-safe manner.
/// </summary>
/// <remarks>
/// This interceptor is useful when you want a single exception interceptor to run for multiple query types
/// without defining separate generic implementations.
///
/// Implementations will receive queries as <see cref="IQuery"/> and results as <see cref="object"/>.
/// For scenarios requiring type-safe handling of a specific query type and result type,
/// consider using <see cref="IQueryExceptionInterceptor{TQuery,TResult}"/> instead.
/// </remarks>
public interface IQueryExceptionInterceptor: IQuery, IAsyncExceptionInterceptor<IQuery>;