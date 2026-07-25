using Ergosfare.Context;
using Ergosfare.Contracts;
using TodoExample.Contracts.Queries;
using TodoExample.Data;
using TodoExample.Domain;

namespace TodoExample.UseCases.Queries.Todos;


public class ListTodosQueryHandler(
    TodoService todoService): IQueryHandler<ListTodosQuery, List<Todo>>
{
    public Task<List<Todo>> HandleAsync(ListTodosQuery _, IExecutionContext context)
    {
        return Task.FromResult(todoService.GetTodos());
    }
}