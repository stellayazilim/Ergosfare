using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Queries.Abstractions;


/// <summary>
/// Represents an exception interceptor for queries.
/// </summary>
/// <remarks>
/// This interface is deprecated. Use the generic versions instead:
/// <see cref="IQueryExceptionInterceptor{TQuery, TResult}"/> or
/// <see cref="IQueryExceptionInterceptor{TQuery, TResult, TModifiedResult}"/>.
/// </remarks>
[Obsolete("Use IQueryExceptionInterceptor<TQuery, TResult> or IQueryExceptionInterceptor<TQuery, TResult, TModifiedResult> instead.")]
public interface IQueryExceptionInterceptor<in TQuery>: IQuery, IAsyncExceptionInterceptor<TQuery, object> 
    where TQuery : IQuery;