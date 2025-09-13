using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Queries.Abstractions;

public interface IQueryExceptionInterceptor: IQuery, IAsyncExceptionInterceptor<IQuery>;