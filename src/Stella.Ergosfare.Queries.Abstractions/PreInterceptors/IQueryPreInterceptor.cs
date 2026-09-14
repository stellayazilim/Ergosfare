using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// Runs before the handler of any query, whatever its type.
/// </summary>
/// <remarks>
/// Because it accepts every query, this contract sees them as <see cref="IQuery"/> and
/// returns <see cref="object"/>. To work with one query type without casting — and to
/// return that type rather than <see cref="object"/> — implement
/// <see cref="IQueryPreInterceptor{TQuery}"/>.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IQueryPreInterceptor: IQuery, IAsyncPreInterceptor<IQuery>;
