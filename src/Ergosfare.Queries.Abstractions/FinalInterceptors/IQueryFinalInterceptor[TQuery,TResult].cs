using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Queries.Abstractions;

public interface IQueryFinalInterceptor<in TQuery, in TResult>:
    IQuery, IAsyncFinalInterceptor<TQuery, TResult> 
    where TQuery : IQuery<TResult>;