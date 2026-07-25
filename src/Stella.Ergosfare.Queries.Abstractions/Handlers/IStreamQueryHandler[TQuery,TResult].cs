using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Queries.Abstractions;


/// <summary>
/// Represents a type-safe asynchronous handler for a stream query of type <typeparamref name="TQuery"/>,
/// producing a stream of results of type <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TQuery">The type of the stream query being handled. Must implement <see cref="IStreamQuery{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The type of results produced by the stream query.</typeparam>
/// <remarks>
/// <para>
/// Implementing this interface allows a handler to process a stream query asynchronously
/// and yield multiple results over time, as part of a reactive query mediation pipeline.
/// </para>
/// <para>
/// Handlers implementing this interface are automatically recognized and invoked
/// by the query mediator when the corresponding stream query type is dispatched.
/// </para>
/// </remarks>
public interface IStreamQueryHandler<in TQuery, out TResult> : 
    IQuery, IStreamHandler<TQuery, TResult> where TQuery : IStreamQuery<TResult>;