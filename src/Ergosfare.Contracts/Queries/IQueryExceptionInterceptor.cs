using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface IQueryExceptionInterceptor: IQuery, IAsyncExceptionInterceptor<IQuery>;