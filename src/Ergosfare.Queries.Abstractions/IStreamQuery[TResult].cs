namespace Ergosfare.Queries.Abstractions;


/// <summary>
/// Represents a stream query message that produces multiple results of type <typeparamref name="TResult"/> over time.
/// </summary>
/// <typeparam name="TResult">The type of results produced by the stream query.</typeparam>
/// <remarks>
/// <para>
/// This interface extends <see cref="IQuery"/> and is intended for reactive or streaming scenarios,
/// where the query yields multiple results asynchronously instead of a single value.
/// </para>
/// <para>
/// Implementing <see cref="IStreamQuery{TResult}"/> allows the query to be registered within
/// the query module and handled by <see cref="IStreamQueryHandler{TQuery, TResult}"/> implementations.
/// </para>
/// </remarks>
public interface IStreamQuery<out TResult>: IQuery;