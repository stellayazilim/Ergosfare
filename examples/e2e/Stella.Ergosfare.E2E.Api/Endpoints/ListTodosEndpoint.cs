using Stella.Ergosfare.E2E.Contracts.Queries;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.MinimalApi;

namespace Stella.Ergosfare.E2E.Api.Endpoints;

public sealed class ListTodosEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/todos", async (IQueryMediator queries) =>
            Results.Ok(await queries.QueryAsync(new ListTodosQuery())));
}
