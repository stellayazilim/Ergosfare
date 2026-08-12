using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.E2E.Contracts.Queries;

/// <summary>Lists every todo, oldest first.</summary>
public sealed record ListTodosQuery : IQuery<IReadOnlyList<TodoDto>>;
