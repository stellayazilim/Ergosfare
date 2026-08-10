using Stella.Ergosfare.E2E.Api.Contracts;
using Stella.Ergosfare.E2E.UseCases;
using Stella.MinimalApi;

namespace Stella.Ergosfare.E2E.Api.Endpoints;

public sealed class StatsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/stats", (TodoStats stats) =>
            Results.Ok(new StatsResponse(stats.Created, stats.Completed)));
}
