using Microsoft.EntityFrameworkCore;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.E2E.Contracts;
using Stella.Ergosfare.E2E.Contracts.Queries;
using Stella.Ergosfare.E2E.Infrastructure;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.E2E.UseCases.Todos;

public sealed class GetTodoHandler(TodoDbContext db) : IQueryHandler<GetTodoQuery, TodoDto?>
{
    public async ValueTask<TodoDto?> HandleAsync(GetTodoQuery query, IExecutionContext context)
    {
        var todo = await db.Todos.FirstOrDefaultAsync(t => t.Id == query.Id, context.CancellationToken);

        return todo is null
            ? null
            : new TodoDto(todo.Id, todo.Title, todo.IsCompleted, todo.CreatedAt);
    }
}
