using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// Runs after the handler of any query, whatever its type.
/// </summary>
/// <remarks>
/// Because it accepts every query, this contract sees the query as <see cref="IQuery"/> and
/// its result as <see cref="object"/>. To work with a typed result, implement
/// <see cref="IQueryPostInterceptor{TQuery, TResult}"/>.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IQueryPostInterceptor: IQuery, IAsyncPostInterceptor<IQuery>;
