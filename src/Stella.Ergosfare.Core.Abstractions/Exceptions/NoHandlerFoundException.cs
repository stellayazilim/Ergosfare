
namespace Stella.Ergosfare.Core.Abstractions.Exceptions;


/// <summary>
/// Thrown when nothing will handle a message: either no handler is registered for its
/// type, or the handlers that are registered are all filtered out of this dispatch.
/// </summary>
/// <remarks>
/// The <see cref="Exception.Message"/> distinguishes the two cases. The exception derives
/// from <see cref="InvalidOperationException"/>, so one catch covers both.
/// </remarks>
public class NoHandlerFoundException : InvalidOperationException
{
    /// <summary>
    /// Initializes the exception for a message type with no registered handler.
    /// </summary>
    /// <param name="messageType">The message type that went unhandled.</param>
    public NoHandlerFoundException(Type messageType)
        : this(messageType, $"Handler for message type '{messageType.Name}' was not found.")
    {
    }

    /// <summary>
    /// Initializes the exception with a message stating why
    /// <paramref name="messageType"/> went unhandled.
    /// </summary>
    /// <param name="messageType">The message type that went unhandled.</param>
    /// <param name="message">The exception message.</param>
    public NoHandlerFoundException(Type messageType, string message) : base(message)
        => MessageType = messageType;

    /// <summary>
    /// The message type that went unhandled.
    /// </summary>
    public Type MessageType { get; }
}
