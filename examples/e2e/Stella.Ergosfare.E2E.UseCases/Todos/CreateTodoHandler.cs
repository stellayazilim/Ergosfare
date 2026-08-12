using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.E2E.Contracts.Commands;
using Stella.Ergosfare.E2E.Domain;
using Stella.Ergosfare.E2E.Infrastructure;

namespace Stella.Ergosfare.E2E.UseCases.Todos;

public sealed class CreateTodoHandler(TodoStore store) : ICommandHandler<CreateTodoCommand, Guid>
{
    public async ValueTask<Guid> HandleAsync(CreateTodoCommand command, ErgosfareContext context)
    {
        var todo = new Todo
        {
            Id = Guid.NewGuid(),
            Title = command.Title,
            IsCompleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await store.AddAsync(todo, context.CancellationToken);

        return todo.Id;
    }
}
