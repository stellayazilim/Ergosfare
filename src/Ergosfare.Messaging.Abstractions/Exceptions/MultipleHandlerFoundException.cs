using System;

namespace Ergosfare.Messaging.Abstractions.Exceptions;

[Serializable]
public class MultipleHandlerFoundException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MultipleHandlerFoundException" /> class with a message
    ///     that includes the name of the message type and the number of handlers found.
    /// </summary>
    /// <param name="messageType">The type of the message for which multiple handlers were found.</param>
    /// <param name="numberOfHandlers">The number of handlers found for the message type.</param>
    /// <remarks>
    ///     The exception message includes the name of the message type and the number of handlers
    ///     to help diagnose the issue.
    /// </remarks>
    public MultipleHandlerFoundException(Type messageType, int numberOfHandlers)
        : base($"{messageType.Name} has {numberOfHandlers} handlers registered.")
    {
    }
}