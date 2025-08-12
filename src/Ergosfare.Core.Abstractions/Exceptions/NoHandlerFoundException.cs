using System;

namespace Ergosfare.Core.Abstractions.Exceptions;

public class NoHandlerFoundException(Type messageType): Exception(
    $"Handler for message type '{messageType.Name}'  was not found.");