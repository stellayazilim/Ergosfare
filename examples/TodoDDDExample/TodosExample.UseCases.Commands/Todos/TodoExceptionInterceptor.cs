using Ergosfare.Context;
using Ergosfare.Contracts;
using TodoExample.Contracts.Commands;
using TodoExample.Domain;

namespace TodosExample.UseCases.Commands.Todos;

public class TodoExceptionInterceptor: ICommandExceptionInterceptor<CreateTodoCommand>
{
    public Task HandleAsync(CreateTodoCommand message, object? result, Exception exception, IExecutionContext context)
    {
        // rethrow diffirent exception to demonstrate
        if (exception is TodoExistException)
        {
            throw new Exception($"Exception intercepted: {nameof(TodoExistException)} thrown while processing ${message.GetType()}");
        }
    
        return Task.CompletedTask;
    }
}