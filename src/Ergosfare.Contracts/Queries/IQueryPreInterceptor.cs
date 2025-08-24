using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface IQueryPreInterceptor: IQuery, IAsyncPreInterceptor<IQuery>;