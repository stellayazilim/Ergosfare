using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;


/// <summary>
/// Represents a non-generic pre-interceptor for queries in the Stella.Ergosfare pipeline.
/// </summary>
/// <remarks>
/// Pre-interceptors run before the main query handler is invoked. They can:
/// <list type="bullet">
/// <item>Inspect or modify the incoming query.</item>
/// <item>Perform validation, logging, or enrichment of the query.</item>
/// <item>Support asynchronous operations via <see cref="IAsyncPreInterceptor{TQuery}"/>.</item>
/// </list>
/// 
/// Use this interface when you do not need a strong typed modified query,
/// and the interceptor should work with any <see cref="IQuery"/>.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IQueryPreInterceptor: IQuery, IAsyncPreInterceptor<IQuery>;