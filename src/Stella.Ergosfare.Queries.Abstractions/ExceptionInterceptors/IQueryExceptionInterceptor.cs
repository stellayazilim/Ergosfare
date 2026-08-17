using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// Handles failures raised while dispatching any query, whatever its type.
/// </summary>
/// <remarks>
/// Because it accepts every query, this contract sees the query as <see cref="IQuery"/> and
/// its result as <see cref="object"/>. Running is what marks the failure handled, so an
/// interceptor this broad handles everything it is registered for — use
/// <see cref="IQueryExceptionInterceptorFor{TException}"/> to narrow it by failure type, or
/// <see cref="IQueryExceptionInterceptor{TQuery, TResult}"/> for a typed result.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IQueryExceptionInterceptor: IQuery, IAsyncExceptionInterceptor<IQuery>;
