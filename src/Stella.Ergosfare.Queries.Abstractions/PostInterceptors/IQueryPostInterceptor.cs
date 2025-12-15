using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// Represents a non-generic post-interceptor for queries, allowing custom logic
/// to execute after any query handlers have been invoked.
/// </summary>
/// <remarks>
/// <para>
/// This interface is a non-generic counterpart of <see cref="IQueryPostInterceptor{TQuery, TResult}"/>,
/// applying to all queries implementing <see cref="IQuery"/>.
/// </para>
/// <para>
/// It inherits from <see cref="IAsyncPostInterceptor{TQuery}"/>, enabling asynchronous
/// post-processing after query execution.
/// </para>
/// <para>
/// Query handlers and queries implementing <see cref="IQuery"/> will automatically
/// recognize this interceptor in the query mediation pipeline.
/// </para>
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IQueryPostInterceptor: IQuery, IAsyncPostInterceptor<IQuery>;