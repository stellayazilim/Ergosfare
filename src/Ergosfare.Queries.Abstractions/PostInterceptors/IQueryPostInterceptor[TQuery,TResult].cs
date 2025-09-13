using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Queries.Abstractions;

public interface IQueryPostInterceptor<in TQuery, in TResult>: IQuery, IAsyncPostInterceptor<TQuery, TResult>
    where TQuery: IQuery<TResult> where TResult: notnull;