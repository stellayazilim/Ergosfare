
namespace Stella.Ergosfare.Core.Abstractions.Exceptions;

/// <summary>
/// Thrown when a type is used as a message but is not one.
/// </summary>
/// <remarks>
/// The framework does not raise this exception; message types are validated at compile
/// time. It is available to hosts and extensions that accept message types at runtime.
/// </remarks>
/// <param name="type">The type that is not a valid message type.</param>
public class InvalidMessageTypeException (Type type): Exception($"Message of type {type} is not a valid message type.");
