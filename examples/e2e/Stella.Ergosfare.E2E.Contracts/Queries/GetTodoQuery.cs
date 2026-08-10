using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.E2E.Contracts.Queries;

/// <summary>Fetches a single todo by id, or null when it does not exist.</summary>
public sealed record GetTodoQuery(Guid Id) : IQuery<TodoDto?>;
