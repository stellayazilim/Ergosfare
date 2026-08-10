namespace Stella.Ergosfare.E2E.Domain;

/// <summary>Raised when a command carries invalid data; the Api maps it to HTTP 400.</summary>
public sealed class TodoValidationException(string message) : Exception(message);
