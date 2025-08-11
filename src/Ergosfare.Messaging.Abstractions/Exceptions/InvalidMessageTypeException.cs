using System;

namespace Ergosfare.Messaging.Abstractions.Exceptions;

public class InvalidMessageTypeException (Type type): Exception($"Message of type {type} is not a valid message type.");