using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;


/// <summary>
/// Handles queries of type <typeparamref name="TQuery"/> by streaming
/// <typeparamref name="TResult"/> items back to the caller.
/// </summary>
/// <typeparam name="TQuery">The stream query type this handler accepts.</typeparam>
/// <typeparam name="TResult">The type of each streamed item.</typeparam>
/// <remarks>
/// Items are produced as the caller enumerates, so the handler body runs after the dispatch
/// call itself has returned. A stream query is served by exactly one handler.
/// </remarks>
public interface IStreamQueryHandler<in TQuery, out TResult> :
    IQuery, IStreamHandler<TQuery, TResult> where TQuery : IStreamQuery<TResult>;
