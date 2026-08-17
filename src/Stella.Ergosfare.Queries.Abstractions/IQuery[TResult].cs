namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// Marks a type as a query whose handler returns a <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TResult">The type the handler returns.</typeparam>
/// <remarks>
/// Declaring the result type on the query means the caller and the handler cannot disagree
/// about it. For a query that yields many results, implement
/// <see cref="IStreamQuery{TResult}"/> instead.
/// </remarks>
public interface IQuery<TResult>: IQuery;
