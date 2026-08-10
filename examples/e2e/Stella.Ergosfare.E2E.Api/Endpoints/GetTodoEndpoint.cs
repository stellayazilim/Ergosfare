using Stella.Ergosfare.E2E.Contracts.Queries;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.MinimalApi;

namespace Stella.Ergosfare.E2E.Api.Endpoints;

public sealed class GetTodoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/todos/{id:guid}", async (Guid id, IQueryMediator queries) =>
        {
            var todo = await queries.QueryAsync(new GetTodoQuery(id));
            return todo is null ? Results.NotFound() : Results.Ok(todo);
        });
}
