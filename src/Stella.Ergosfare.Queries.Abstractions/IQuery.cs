using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// Marks a type as a query: a message sent to exactly one handler to read something.
/// </summary>
/// <remarks>
/// A query is expected to read rather than change state. The interface declares no members;
/// implement <see cref="IQuery{TResult}"/> to declare what the query returns, or
/// <see cref="IStreamQuery{TResult}"/> to stream results back.
/// </remarks>
public interface IQuery: IMessage;
