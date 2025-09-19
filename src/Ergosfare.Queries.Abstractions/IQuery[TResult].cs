namespace Ergosfare.Queries.Abstractions;

/// <summary>
/// Represents a type-safe query message that produces a result of type <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TResult">The type of result returned by the query.</typeparam>
/// <remarks>
/// <para>
/// This interface extends <see cref="IQuery"/> and is intended for queries that return
/// a specific result type when handled by a query handler.
/// </para>
/// <para>
/// Implementing <see cref="IQuery{TResult}"/> allows the query to be registered within
/// the query module and processed by type-safe query handlers and interceptors.
/// </para>
/// </remarks>
public interface IQuery<TResult>: IQuery;