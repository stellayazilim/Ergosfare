namespace Stella.Ergosfare.E2E.Api.Contracts;

// Named response types rather than anonymous objects: the JSON source generator needs a
// declared type to emit a serializer for, and an anonymous type has none to give it.

/// <summary>Body of the readiness probe.</summary>
public sealed record HealthResponse(string Status);

/// <summary>Body returned by <c>POST /todos</c>.</summary>
public sealed record CreatedTodoResponse(Guid Id);

/// <summary>Body returned by <c>GET /stats</c> — the event pipeline's observable trace.</summary>
public sealed record StatsResponse(int Created, int Completed);
