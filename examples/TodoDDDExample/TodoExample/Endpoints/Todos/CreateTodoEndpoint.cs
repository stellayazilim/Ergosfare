using Ergosfare.Commands.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Stella.MinimalApi;
using TodoExample.Contracts.Commands;

namespace TodoExample.Endpoints;

public class CreateTodoEndpoint: IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/todos", async ([FromBody] CreateTodoCommand command, ICommandMediator mediator) =>
        {
           await mediator.SendAsync(command);
        });
    }
}