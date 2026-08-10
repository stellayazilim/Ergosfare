using System.Text.Json.Serialization;
using Stella.Ergosfare.E2E.Contracts;
using Stella.Ergosfare.E2E.Contracts.Commands;

namespace Stella.Ergosfare.E2E.Api.Contracts;

/// <summary>
/// Compile-time serializers for everything that crosses the wire. Without this the runtime
/// would build them by reflection, which NativeAOT cannot do.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CreateTodoCommand))]
[JsonSerializable(typeof(TodoDto))]
[JsonSerializable(typeof(IReadOnlyList<TodoDto>))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(CreatedTodoResponse))]
[JsonSerializable(typeof(StatsResponse))]
public sealed partial class ApiJsonContext : JsonSerializerContext;
