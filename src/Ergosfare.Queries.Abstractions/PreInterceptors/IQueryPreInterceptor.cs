using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Queries.Abstractions;

public interface IQueryPreInterceptor: IQuery, IAsyncPreInterceptor<IQuery>;