using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Queries.Abstractions;


/// <summary>
/// Represents a pre-interceptor for query messages in the Ergosfare pipeline.
/// </summary>
/// <typeparam name="TQuery">
/// The type of query to be intercepted. Must implement <see cref="IQuery"/>.
/// </typeparam>
/// <remarks>
/// Pre-interceptors execute before the main query handler is invoked. They can be used to:
/// <list type="bullet">
/// <item>Validate or modify the query.</item>
/// <item>Perform logging or auditing.</item>
/// <item>Inject additional context or metadata.</item>
/// </list>
/// This interface inherits from <see cref="IAsyncPreInterceptor{TQuery}"/> to allow asynchronous
/// pre-processing, and from <see cref="IQuery"/> to associate it with a specific query type.
/// </remarks>
public interface IQueryPreInterceptor<in TQuery>: IQuery, IAsyncPreInterceptor<TQuery> where TQuery : IQuery;