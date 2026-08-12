using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.E2E.Api.Contracts;
using Stella.Ergosfare.E2E.Contracts.Commands;
using Stella.Ergosfare.E2E.Contracts.Events;
using Stella.Ergosfare.Events.Abstractions;
using Stella.MinimalApi;

namespace Stella.Ergosfare.E2E.Api.Endpoints;

public sealed class CreateTodoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/todos", async (
            CreateTodoCommand command, ICommandMediator commands, IEventMediator events) =>
        {
            var id = await commands.SendAsync<Guid>(command);
            await events.PublishAsync(new TodoCreatedEvent(id, command.Title));

            return Results.Created($"/todos/{id}", new CreatedTodoResponse(id));
        });
}
