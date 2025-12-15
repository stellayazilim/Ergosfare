using System;

namespace Stella.Ergosfare.Core.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when a message of an invalid type is encountered.
/// </summary>
/// <param name="type">The type of the invalid message.</param>
public class InvalidMessageTypeException (Type type): Exception($"Message of type {type} is not a valid message type.");