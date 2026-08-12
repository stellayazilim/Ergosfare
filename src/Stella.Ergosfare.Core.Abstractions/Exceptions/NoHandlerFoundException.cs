
namespace Stella.Ergosfare.Core.Abstractions.Exceptions;


/// <summary>
/// Exception thrown when nothing will handle a message: either the message type has no
/// descriptor at all, or it has one whose handlers are every one excluded from this
/// dispatch. The <see cref="Exception.Message"/> says which.
/// </summary>
/// <remarks>
/// Derives from <see cref="InvalidOperationException"/> because the second case used to
/// throw one of those directly, so callers catching it keep catching it. Callers wanting
/// the specific failure now catch one type for both cases instead of two.
/// </remarks>
public class NoHandlerFoundException : InvalidOperationException
{
    /// <summary>
    /// Initializes the exception for a message type nothing is registered against.
    /// </summary>
    /// <param name="messageType">The type of the message for which no handler was found.</param>
    public NoHandlerFoundException(Type messageType)
        : this(messageType, $"Handler for message type '{messageType.Name}' was not found.")
    {
    }

    /// <summary>
    /// Initializes the exception with a message describing why nothing will handle
    /// <paramref name="messageType"/>.
    /// </summary>
    /// <param name="messageType">The type of the message for which no handler was found.</param>
    /// <param name="message">The message that describes the error.</param>
    public NoHandlerFoundException(Type messageType, string message) : base(message)
        => MessageType = messageType;

    /// <summary>
    /// Gets the type of the message that caused the exception.
    /// </summary>
    public Type MessageType { get; }
}
