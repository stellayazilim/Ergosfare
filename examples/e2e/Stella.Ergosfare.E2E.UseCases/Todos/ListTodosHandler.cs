using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.E2E.Contracts;
using Stella.Ergosfare.E2E.Contracts.Queries;
using Stella.Ergosfare.E2E.Infrastructure;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.E2E.UseCases.Todos;

public sealed class ListTodosHandler(TodoStore store) : IQueryHandler<ListTodosQuery, IReadOnlyList<TodoDto>>
{
    public async ValueTask<IReadOnlyList<TodoDto>> HandleAsync(ListTodosQuery query, ErgosfareContext context)
    {
        var todos = await store.ListAsync(context.CancellationToken);

        return todos
            .Select(todo => new TodoDto(todo.Id, todo.Title, todo.IsCompleted, todo.CreatedAt))
            .ToArray();
    }
}
