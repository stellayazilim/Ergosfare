using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface IQueryHandler<in TQuery,TResult>: IQuery, IAsyncHandler<TQuery, TResult> 
    where TQuery : IQuery<TResult>;