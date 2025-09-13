using Ergosfare.Commands.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Stella.MinimalApi;
using TodoExample.Contracts.Commands;

namespace TodoExample.Endpoints;

public class UpdateTodoEndpoint: IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("/todos", async (
            [FromBody] UpdateTodoCommand command, ICommandMediator mediator) =>
        {
            await mediator.SendAsync(command);
        });
    }
}