using Ergosfare.Commands.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Stella.MinimalApi;
using TodoExample.Contracts.Commands;

namespace TodoExample.Endpoints;

public class DeleteTodoEndpoint: IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/todos", async (
            [FromBody] DeleteTodoCommand command,
            ICommandMediator mediator
        ) =>
        {
             await mediator.SendAsync(command);
        });
    }
}