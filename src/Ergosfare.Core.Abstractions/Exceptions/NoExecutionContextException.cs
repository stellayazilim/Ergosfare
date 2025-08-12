using System;

namespace Ergosfare.Core.Abstractions.Exceptions;

public class NoExecutionContextException(string? message = "No execution context is set") : Exception(message);