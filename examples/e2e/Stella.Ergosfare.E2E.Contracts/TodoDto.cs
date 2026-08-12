namespace Stella.Ergosfare.E2E.Contracts;

/// <summary>The read model returned by the query side.</summary>
public sealed record TodoDto(Guid Id, string Title, bool IsCompleted, DateTimeOffset CreatedAt);
