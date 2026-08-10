using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.E2E.Contracts.Commands;
using Stella.Ergosfare.E2E.Domain;
using Stella.Ergosfare.E2E.Infrastructure;

namespace Stella.Ergosfare.E2E.UseCases.Todos;

public sealed class CreateTodoHandler(TodoDbContext db) : ICommandHandler<CreateTodoCommand, Guid>
{
    public async ValueTask<Guid> HandleAsync(CreateTodoCommand command, IExecutionContext context)
    {
        var todo = new Todo
        {
            Id = Guid.NewGuid(),
            Title = command.Title,
            IsCompleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Todos.Add(todo);
        await db.SaveChangesAsync(context.CancellationToken);

        return todo.Id;
    }
}
