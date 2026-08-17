using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// Handles failures raised while dispatching a <typeparamref name="TQuery"/> and supplies
/// the result the caller receives instead.
/// </summary>
/// <typeparam name="TQuery">The query type this interceptor accepts.</typeparam>
/// <typeparam name="TResult">The result type the query declares.</typeparam>
/// <remarks>
/// <typeparamref name="TQuery"/> is contravariant, so an interceptor written against a base
/// query type also runs for the queries derived from it; <typeparamref name="TResult"/>
/// stays invariant because it is returned. Implement
/// <see cref="IQueryExceptionInterceptorFor{TQuery, TResult, TException}"/> to accept only
/// certain failures.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IQueryExceptionInterceptor<in TQuery, TResult>
    : IQuery, IAsyncExceptionInterceptor<TQuery, TResult>
    where TQuery : IQuery<TResult>
    where TResult : notnull
{
    /// <summary>
    /// Forwards the core contract to the typed method below.
    /// </summary>
    /// <param name="query">The query whose dispatch failed.</param>
    /// <param name="result">The result produced so far, if any.</param>
    /// <param name="exception">The failure being handled.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>The result the typed method returned.</returns>
    async ValueTask<object?> IAsyncExceptionInterceptor<TQuery, TResult>.HandleAsync(
        TQuery query, TResult? result, Exception exception, ErgosfareContext context)
        => await HandleAsync(query, result, exception, context);

    /// <summary>
    /// Handles <paramref name="exception"/> and produces the result to continue with.
    /// </summary>
    /// <param name="query">The query whose dispatch failed.</param>
    /// <param name="result">
    /// The result produced before the failure, which is the result type's default when the
    /// handler itself failed.
    /// </param>
    /// <param name="exception">The failure being handled.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>The result the caller receives.</returns>
    /// <remarks>
    /// Running this method is what marks the failure handled, and a handled failure has to
    /// leave a result behind: the call site locked the result type when it dispatched. To
    /// leave a failure for the caller, do not accept it — a failure no interceptor accepts
    /// reaches the caller unchanged.
    /// </remarks>
    new ValueTask<TResult> HandleAsync(TQuery query, TResult? result, Exception exception, ErgosfareContext context);
}
