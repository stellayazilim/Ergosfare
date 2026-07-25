namespace Stella.Ergosfare.E2E.Domain;

/// <summary>Raised when a todo referenced by id does not exist; the Api maps it to HTTP 404.</summary>
public sealed class TodoNotFoundException(Guid id) : Exception($"todo {id} was not found");
