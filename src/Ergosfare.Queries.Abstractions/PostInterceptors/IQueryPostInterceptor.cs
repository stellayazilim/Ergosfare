using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Queries.Abstractions;

public interface IQueryPostInterceptor: IQuery, IAsyncPostInterceptor<IQuery>;