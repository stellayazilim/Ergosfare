using System;

namespace Ergosfare.Core.Abstractions.Exceptions;

public class InvalidMessageTypeException (Type type): Exception($"Message of type {type} is not a valid message type.");