using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// Runs once the pipeline of a <typeparamref name="TQuery"/> has settled, reading its
/// result as a <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TQuery">The query type this interceptor accepts.</typeparam>
/// <typeparam name="TResult">The result type the query declares.</typeparam>
/// <remarks>
/// It runs after the pre-, post- and exception stages and sees the query, the result and
/// any failure, but cannot change the outcome. A pipeline stopped by <c>context.Abort()</c>
/// runs no final interceptors.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IQueryFinalInterceptor<in TQuery, in TResult>: IQuery, IAsyncFinalInterceptor<TQuery, TResult>
    where TQuery : IQuery<TResult>;
