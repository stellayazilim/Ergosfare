using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Queries.Abstractions;

public interface IQueryPostInterceptor<in TQuery>: IQuery, IAsyncPostInterceptor<IQuery> where TQuery : IQuery;