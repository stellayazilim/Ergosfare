using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.E2E.Contracts;
using Stella.Ergosfare.E2E.Contracts.Queries;
using Stella.Ergosfare.E2E.Infrastructure;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.E2E.UseCases.Todos;

public sealed class GetTodoHandler(TodoStore store) : IQueryHandler<GetTodoQuery, TodoDto?>
{
    public async ValueTask<TodoDto?> HandleAsync(GetTodoQuery query, ErgosfareContext context)
    {
        var todo = await store.FindAsync(query.Id, context.CancellationToken);

        return todo is null
            ? null
            : new TodoDto(todo.Id, todo.Title, todo.IsCompleted, todo.CreatedAt);
    }
}
