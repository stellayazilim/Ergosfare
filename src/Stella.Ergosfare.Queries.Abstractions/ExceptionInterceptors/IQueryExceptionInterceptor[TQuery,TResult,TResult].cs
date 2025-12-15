using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;


/// <summary>
/// Represents a type-safe exception interceptor for queries with a strongly-typed result.
/// This interceptor can modify the result or replace it entirely if an exception occurs.
/// </summary>
/// <typeparam name="TQuery">The type of the query being intercepted. Must implement <see cref="IQuery{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The strongly-typed result of the query.</typeparam>
/// <typeparam name="TModifiedResult">
/// The type of the result that this interceptor can return. Must be compatible with <typeparamref name="TResult"/>.
/// </typeparam>
// ReSharper disable once UnusedType.Global
public interface IQueryExceptionInterceptor<in TQuery, in TResult, TModifiedResult>
    : IQuery,IAsyncExceptionInterceptor<TQuery, TResult> 
    where TQuery : IQuery<TResult>
    where TResult : notnull
    where TModifiedResult : TResult
{
    /// <inheritdoc />
    async Task<object> IAsyncExceptionInterceptor<TQuery, TResult>.HandleAsync(
        TQuery query, TResult? result, Exception exception, IExecutionContext context 
        ) => (await HandleAsync(query, result, exception, context))!;
    
    
    /// <summary>
    /// Handles an exception asynchronously for the specified query and its strongly-typed result.
    /// </summary>
    /// <param name="query">The query that was being processed when the exception occurred.</param>
    /// <param name="result">
    /// The current result of the query produced by the pipeline before this interceptor. 
    /// Can be modified or replaced by the interceptor.
    /// </param>
    /// <param name="exception">The exception that occurred during query processing.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
    /// The <paramref name="result"/> parameter contains the value produced by the pipeline 
    /// before this interceptor executes. The returned <typeparamref name="TModifiedResult"/> 
    /// may modify or replace this result, which will continue through the pipeline.
    /// </returns>
    new Task<TModifiedResult?> HandleAsync(TQuery query, TResult? result, Exception exception, IExecutionContext context); 
}