namespace Ergosfare.Core.Context;

public class NoExecutionContextException(string? message = "No execution context is set") : Exception(message);