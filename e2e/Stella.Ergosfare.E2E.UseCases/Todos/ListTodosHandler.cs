using Microsoft.EntityFrameworkCore;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.E2E.Contracts;
using Stella.Ergosfare.E2E.Contracts.Queries;
using Stella.Ergosfare.E2E.Infrastructure;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.E2E.UseCases.Todos;

public sealed class ListTodosHandler(TodoDbContext db) : IQueryHandler<ListTodosQuery, IReadOnlyList<TodoDto>>
{
    public async ValueTask<IReadOnlyList<TodoDto>> HandleAsync(ListTodosQuery query, IExecutionContext context)
    {
        return await db.Todos
            .OrderBy(t => t.CreatedAt)
            .Select(t => new TodoDto(t.Id, t.Title, t.IsCompleted, t.CreatedAt))
            .ToListAsync(context.CancellationToken);
    }
}
