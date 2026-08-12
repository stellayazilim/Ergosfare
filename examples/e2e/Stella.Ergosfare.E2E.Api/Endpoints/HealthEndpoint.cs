using Stella.Ergosfare.E2E.Api.Contracts;
using Stella.MinimalApi;

namespace Stella.Ergosfare.E2E.Api.Endpoints;

/// <summary>Readiness probe the e2e runner polls before firing the .http suite.</summary>
public sealed class HealthEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/health", () => Results.Ok(new HealthResponse("ok")));
}
