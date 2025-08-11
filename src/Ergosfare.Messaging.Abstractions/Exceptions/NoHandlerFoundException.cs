using System;

namespace Ergosfare.Messaging.Abstractions.Exceptions;

public class NoHandlerFoundException(Type messageType): Exception(
    $"Handler for message type '{messageType.Name}'  was not found.");