using Ergosfare.Context;
using Ergosfare.Contracts;
using TodoExample.Contracts.Commands;
using TodoExample.Data;
using TodoExample.Domain;

namespace TodosExample.UseCases.Commands.Todos;

public class UpdateTodoCommandHandler(
    TodoService todoService): ICommandHandler<UpdateTodoCommand>
{
    public Task HandleAsync(UpdateTodoCommand command, IExecutionContext context)
    {
        todoService.UpdateTodo(new Todo()
        {
            Name = command.Name,
            IsCompleted = command.IsCompleted
        });
        
        return Task.CompletedTask;
    }
}