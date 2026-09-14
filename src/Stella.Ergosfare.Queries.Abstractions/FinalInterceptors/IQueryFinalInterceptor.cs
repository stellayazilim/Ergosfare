using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// Runs once the pipeline of any query has settled, whatever its type.
/// </summary>
/// <remarks>
/// Use this for work that applies across query types — logging, metrics, cleanup. It
/// observes the outcome and cannot change it, and a pipeline stopped by
/// <c>context.Abort()</c> runs no final interceptors. For a typed query and result,
/// implement <see cref="IQueryFinalInterceptor{TQuery, TResult}"/>.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IQueryFinalInterceptor: IQuery, IAsyncFinalInterceptor<IQuery>;
