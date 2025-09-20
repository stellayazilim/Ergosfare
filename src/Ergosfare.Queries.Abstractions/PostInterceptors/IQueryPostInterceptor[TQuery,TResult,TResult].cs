using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Queries.Abstractions;


/// <summary>
/// Represents a type-safe post-interceptor for queries in the Ergosfare pipeline.
/// </summary>
/// <typeparam name="TQuery">The type of query being intercepted.</typeparam>
/// <typeparam name="TResult">The result type produced by the query handler.</typeparam>
/// <typeparam name="TModifiedResult">
/// The type of the modified result returned by this interceptor. Must be compatible with <typeparamref name="TResult"/>.
/// </typeparam>
/// <remarks>
/// Post-interceptors execute after the main query handler has processed the query. They allow you to:
/// <list type="bullet">
/// <item>Inspect, modify, or replace the query result with a strongly-typed <typeparamref name="TModifiedResult"/>.</item>
/// <item>Perform logging, metrics collection, or additional asynchronous side-effects.</item>
/// <item>Integrate seamlessly into the pipeline while preserving type safety.</item>
/// </list>
///
/// The <see cref="IAsyncPostInterceptor{TQuery, TResult}.HandleAsync"/> implementation is automatically
/// forwarded to the type-safe <see cref="HandleAsync"/> method, returning a boxed <see cref="object"/>.
/// </remarks>
public interface IQueryPostInterceptor<in TQuery,in TResult, TModifiedResult>:
    IQuery, IAsyncPostInterceptor<TQuery, TResult>
    where TQuery: IQuery 
    where TResult: notnull
    where TModifiedResult: TResult 
{

    /// <summary>
    /// Explicit implementation of <see cref="IAsyncPostInterceptor{TQuery, TResult}.HandleAsync"/> that
    /// forwards the call to the type-safe <see cref="HandleAsync"/> method.
    /// </summary>
    /// <param name="query">The query being processed.</param>
    /// <param name="result">The result produced by the main query handler. May be <c>null</c>.</param>
    /// <param name="executionContext">The execution context for this pipeline operation.</param>
    /// <returns>
    /// A <see cref="Task{Object}"/> representing the asynchronous operation, returning the modified result
    /// boxed as <see cref="object"/>. This allows the type-safe interceptor to integrate with non-generic pipeline logic.
    /// </returns>
    /// <remarks>
    /// This method should not be called directly. Pipeline infrastructure calls it to maintain polymorphism
    /// between type-safe and non-type-safe interceptors.
    /// </remarks>
    async Task<object> IAsyncPostInterceptor<TQuery, TResult>.HandleAsync(TQuery query, TResult? result,
        IExecutionContext executionContext)
    {
        return (await HandleAsync(query, result, executionContext))!;
    }

    /// <summary>
    /// Handles the post-interception for the query and returns a modified result.
    /// </summary>
    /// <param name="query">The query being processed.</param>
    /// <param name="result">The result produced by the main query handler. May be <c>null</c> in some scenarios.</param>
    /// <param name="executionContext">The execution context of the pipeline.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation, returning the modified result.</returns>
    new Task<TModifiedResult?> HandleAsync(TQuery query, TResult? result, IExecutionContext executionContext);
}