using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface IQueryPostInterceptor: IQuery, IAsyncPostInterceptor<IQuery>;