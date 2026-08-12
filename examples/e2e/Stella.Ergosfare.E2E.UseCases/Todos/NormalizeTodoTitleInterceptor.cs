using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.E2E.Contracts.Commands;
using Stella.Ergosfare.E2E.Domain;

namespace Stella.Ergosfare.E2E.UseCases.Todos;

/// <summary>
/// Pre-interceptor for <see cref="CreateTodoCommand"/>: trims the title and rejects an empty
/// one before the handler runs. Returning a rewritten command exercises the pre-stage's
/// message-replacement contract; the throw exercises exception propagation to the caller.
/// </summary>
[Weight(100)]
public sealed class NormalizeTodoTitleInterceptor : ICommandPreInterceptor<CreateTodoCommand>
{
    public ValueTask<CreateTodoCommand> HandleAsync(CreateTodoCommand command, ErgosfareContext context)
    {
        var title = command.Title?.Trim() ?? string.Empty;

        if (title.Length == 0)
        {
            throw new TodoValidationException("title must not be empty");
        }

        // Whatever we return here is the command the handler sees next — strongly typed.
        // ReSharper disable once WithExpressionModifiesAllMembers
        return ValueTask.FromResult(command with { Title = title });
    }
}
