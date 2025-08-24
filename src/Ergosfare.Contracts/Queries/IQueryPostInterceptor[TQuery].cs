using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface IQueryPostInterceptor<in TQuery>: IQuery, IAsyncPostInterceptor<IQuery> where TQuery : IQuery;