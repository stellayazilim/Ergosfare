using System;

namespace Stella.Ergosfare.Core.Abstractions.Exceptions;


/// <summary>
/// Exception thrown when no handler is found for a specific message type.
/// </summary>
/// <param name="messageType">The type of the message for which no handler was found.</param>
public class NoHandlerFoundException(Type messageType): Exception(
    $"Handler for message type '{messageType.Name}'  was not found.");