using System;

namespace Ergosfare.Core.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when multiple handlers are found for a message that expects only one.
/// </summary>
/// <param name="messageType">The type of the message for which multiple handlers were found.</param>
/// <param name="numberOfHandlers">The number of handlers found for the message type.</param>
[Serializable]
public class MultipleHandlerFoundException(Type messageType, int numberOfHandlers) : Exception($"{messageType.Name} has {numberOfHandlers} handlers registered.")
{
    /// <summary>
    /// Gets the type of the message that caused the exception.
    /// </summary>
    public Type MessageType => messageType;
    
    /// <summary>
    /// Gets the number of handlers found for the message type.
    /// </summary>
    public int NumberOfHandlers => numberOfHandlers;
}