using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// Represents a non-generic final interceptor for queries, allowing custom logic
/// to execute after all query handlers and other interceptors have completed.
/// </summary>
/// <remarks>
/// <para>
/// This interface applies to all queries implementing <see cref="IQuery"/>.
/// </para>
/// <para>
/// It inherits from the result-agnostic <see cref="IAsyncFinalInterceptor{TMessage}"/>,
/// enabling asynchronous final processing of queries after they are dispatched to their
/// handlers. The result-agnostic base is deliberate: a result-typed base (the previous
/// <c>IAsyncFinalInterceptor&lt;IQuery, object&gt;</c>) is invisible to the pipeline's
/// pattern match whenever the query result is a value type, so the final stage failed
/// with <see cref="System.NotSupportedException"/> for such queries the moment it ran.
/// For a strongly-typed result use <see cref="IQueryFinalInterceptor{TQuery, TResult}"/>.
/// </para>
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IQueryFinalInterceptor: IQuery, IAsyncFinalInterceptor<IQuery>;