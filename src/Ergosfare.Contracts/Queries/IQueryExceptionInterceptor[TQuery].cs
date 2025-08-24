using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface IQueryExceptionInterceptor<in TQuery>: IQuery, IAsyncExceptionInterceptor<TQuery, object> where TQuery : IQuery;