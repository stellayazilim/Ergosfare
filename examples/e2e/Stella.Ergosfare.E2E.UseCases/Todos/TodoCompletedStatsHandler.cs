using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.E2E.Domain;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.E2E.UseCases.Todos;

/// <summary>
/// Handles the <see cref="TodoCompleted"/> POCO domain event. It handles a plain object
/// that never implements <c>IEvent</c> — the generator still discovers this handler because
/// <see cref="IEventHandler{TEvent}"/> itself carries the event marker.
/// </summary>
public sealed class TodoCompletedStatsHandler(TodoStats stats) : IEventHandler<TodoCompleted>
{
    public ValueTask HandleAsync(TodoCompleted @event, ErgosfareContext context)
    {
        stats.IncrementCompleted();
        return ValueTask.CompletedTask;
    }
}
