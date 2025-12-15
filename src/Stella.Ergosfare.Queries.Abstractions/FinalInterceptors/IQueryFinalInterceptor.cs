using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// Represents a non-generic final interceptor for queries, allowing custom logic
/// to execute after all query handlers and other interceptors have completed.
/// </summary>
/// <remarks>
/// <para>
/// This interface is a non-generic version of <see cref="IQueryFinalInterceptor{TQuery}"/>,
/// applying to all queries implementing <see cref="IQuery"/>.
/// </para>
/// <para>
/// It inherits from <see cref="IAsyncFinalInterceptor{TQuery, TResult}"/>, enabling
/// asynchronous final processing of queries after they are dispatched to their handlers.
/// </para>
/// <para>
/// Query handlers and messages that implement <see cref="IQuery"/> will recognize
/// this interceptor automatically in the query mediation pipeline.
/// </para>
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IQueryFinalInterceptor: IQuery, IAsyncFinalInterceptor<IQuery, object>;