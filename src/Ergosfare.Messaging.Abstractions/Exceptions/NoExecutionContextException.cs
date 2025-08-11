using System;

namespace Ergosfare.Messaging.Abstractions.Exceptions;

public class NoExecutionContextException(string? message = "No execution context is set") : Exception(message);