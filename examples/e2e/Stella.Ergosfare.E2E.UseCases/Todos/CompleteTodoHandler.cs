using Microsoft.EntityFrameworkCore;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.E2E.Contracts.Commands;
using Stella.Ergosfare.E2E.Domain;
using Stella.Ergosfare.E2E.Infrastructure;

namespace Stella.Ergosfare.E2E.UseCases.Todos;

public sealed class CompleteTodoHandler(TodoDbContext db) : ICommandHandler<CompleteTodoCommand>
{
    public async ValueTask HandleAsync(CompleteTodoCommand command, IExecutionContext context)
    {
        var todo = await db.Todos.FirstOrDefaultAsync(t => t.Id == command.Id, context.CancellationToken)
                   ?? throw new TodoNotFoundException(command.Id);

        // Behavior on the aggregate raises the TodoCompleted domain event; the DbContext
        // publishes it when SaveChanges commits.
        todo.Complete();
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
