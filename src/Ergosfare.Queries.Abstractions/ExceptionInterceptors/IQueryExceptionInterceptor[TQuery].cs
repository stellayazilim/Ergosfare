using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Queries.Abstractions;

public interface IQueryExceptionInterceptor<in TQuery>: IQuery, IAsyncExceptionInterceptor<TQuery, object> where TQuery : IQuery;