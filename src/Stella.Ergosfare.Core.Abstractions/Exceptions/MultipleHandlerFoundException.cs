
namespace Stella.Ergosfare.Core.Abstractions.Exceptions;

/// <summary>
/// Thrown when a message that admits exactly one handler has several registered against
/// it — a command or query with more than one handler at the level that serves it.
/// </summary>
/// <remarks>
/// Handlers registered for the message type itself are considered first; only if there are
/// none does the dispatch consider handlers registered for a base type. The contest is
/// therefore always within one level, and the count reported is that level's.
/// </remarks>
/// <param name="messageType">The message type with the contested handlers.</param>
/// <param name="numberOfHandlers">How many handlers were registered at the level that serves it.</param>
[Serializable]
public class MultipleHandlerFoundException(Type messageType, int numberOfHandlers) : Exception($"{messageType.Name} has {numberOfHandlers} handlers registered.")
{
    /// <summary>
    /// The message type with the contested handlers.
    /// </summary>
    public Type MessageType => messageType;

    /// <summary>
    /// How many handlers were registered at the level that serves the message.
    /// </summary>
    public int NumberOfHandlers => numberOfHandlers;
}
