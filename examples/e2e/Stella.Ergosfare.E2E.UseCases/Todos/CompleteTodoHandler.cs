using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.E2E.Contracts.Commands;
using Stella.Ergosfare.E2E.Domain;
using Stella.Ergosfare.E2E.Infrastructure;

namespace Stella.Ergosfare.E2E.UseCases.Todos;

public sealed class CompleteTodoHandler(TodoStore store) : ICommandHandler<CompleteTodoCommand>
{
    public async ValueTask HandleAsync(CompleteTodoCommand command, IExecutionContext context)
    {
        var todo = await store.FindAsync(command.Id, context.CancellationToken)
                   ?? throw new TodoNotFoundException(command.Id);

        // Behavior on the aggregate raises the TodoCompleted domain event; the store
        // publishes it once the write commits.
        todo.Complete();
        await store.UpdateAsync(todo, context.CancellationToken);
    }
}
