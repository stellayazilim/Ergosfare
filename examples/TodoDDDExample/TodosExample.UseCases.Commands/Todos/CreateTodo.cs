using Ergosfare.Context;
using Ergosfare.Contracts;
using TodoExample.Contracts.Commands;
using TodoExample.Data;
using TodoExample.Domain;

namespace TodosExample.UseCases.Commands.Todos;

public class CreateTodoCommandHandler(
    TodoService todoService): ICommandHandler<CreateTodoCommand>
{
    public Task HandleAsync(CreateTodoCommand command, IExecutionContext context)
    {
 
        todoService.AddTodo(new Todo()
        {
            Name = command.Name,
            IsCompleted = command.IsCompleted 
        });
  
        return Task.CompletedTask;
    }
}