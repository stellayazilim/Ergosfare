using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.E2E.Contracts.Commands;
using Stella.MinimalApi;

namespace Stella.Ergosfare.E2E.Api.Endpoints;

public sealed class CompleteTodoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/todos/{id:guid}/complete", async (Guid id, ICommandMediator commands) =>
        {
            await commands.SendAsync(new CompleteTodoCommand(id));
            return Results.NoContent();
        });
}
