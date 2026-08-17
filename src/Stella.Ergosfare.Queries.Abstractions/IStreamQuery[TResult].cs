namespace Stella.Ergosfare.Queries.Abstractions;


/// <summary>
/// Marks a type as a query whose handler streams <typeparamref name="TResult"/> items back
/// to the caller.
/// </summary>
/// <typeparam name="TResult">The type of each streamed item.</typeparam>
/// <remarks>
/// Use this where results arrive over time rather than all at once. Such a query is served
/// by an <see cref="IStreamQueryHandler{TQuery, TResult}"/>, and items are produced as the
/// caller enumerates them.
/// </remarks>
public interface IStreamQuery<out TResult>: IQuery;
