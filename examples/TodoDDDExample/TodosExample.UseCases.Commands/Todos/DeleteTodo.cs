using Ergosfare.Context;
using Ergosfare.Contracts;
using TodoExample.Contracts.Commands;
using TodoExample.Data;
using TodoExample.Domain;

namespace TodosExample.UseCases.Commands.Todos;

public class DeleteTodoCommandHandler
    (TodoService todoService): ICommandHandler<DeleteTodoCommand>
{
    public Task HandleAsync(DeleteTodoCommand command, IExecutionContext context)
    {
        todoService.RemoveTodo(new Todo()
        {
            Name = command.Name
        });
        
        return Task.CompletedTask;
    }
}