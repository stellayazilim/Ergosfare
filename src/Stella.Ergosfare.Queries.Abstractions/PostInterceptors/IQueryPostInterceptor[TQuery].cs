using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// Runs after the handler of a <typeparamref name="TQuery"/>, without naming the result
/// type.
/// </summary>
/// <typeparam name="TQuery">The query type this interceptor accepts.</typeparam>
/// <remarks>
/// Use this where the work applies to any result — logging or metrics, say — and the result
/// arrives as <see cref="object"/>. To read or replace a typed result, implement
/// <see cref="IQueryPostInterceptor{TQuery, TResult}"/>.
/// </remarks>
public interface IQueryPostInterceptor<in TQuery>: IQuery, IAsyncPostInterceptor<TQuery> where TQuery : IQuery;
