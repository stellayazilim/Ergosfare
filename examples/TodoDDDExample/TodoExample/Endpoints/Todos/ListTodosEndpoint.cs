using Ergosfare.Queries.Abstractions;
using Stella.MinimalApi;
using TodoExample.Contracts.Queries;

namespace TodoExample.Endpoints;

public class ListTodosEndpoint: IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/todos", (IQueryMediator mediator) => mediator.QueryAsync(new ListTodosQuery()));
    }
}