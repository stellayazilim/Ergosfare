using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.E2E.Contracts.Events;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.E2E.UseCases.Todos;

/// <summary>Event handler that bumps the created-todo counter on every broadcast.</summary>
public sealed class TodoCreatedStatsHandler(TodoStats stats) : IEventHandler<TodoCreatedEvent>
{
    public ValueTask HandleAsync(TodoCreatedEvent @event, ErgosfareContext context)
    {
        stats.IncrementCreated();
        return ValueTask.CompletedTask;
    }
}
