using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface IQueryPreInterceptor<in TQuery>: IQuery, IAsyncPreInterceptor<TQuery> where TQuery : IQuery;