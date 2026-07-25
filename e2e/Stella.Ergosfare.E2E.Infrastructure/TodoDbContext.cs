using Microsoft.EntityFrameworkCore;
using Stella.Ergosfare.E2E.Domain;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.E2E.Infrastructure;

public sealed class TodoDbContext(DbContextOptions<TodoDbContext> options, IEventMediator events)
    : DbContext(options)
{
    public DbSet<Todo> Todos => Set<Todo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Todo>(todo =>
        {
            todo.HasKey(t => t.Id);
            todo.Property(t => t.Title).IsRequired();

            // Domain events are behavior, not persisted state.
            todo.Ignore(t => t.DomainEvents);

            // SQLite cannot ORDER BY a DateTimeOffset column. We only ever store UTC, so
            // persist it as a UTC DateTime (stored as orderable ISO-8601 text) and rehydrate
            // it back to a zero-offset DateTimeOffset.
            todo.Property(t => t.CreatedAt).HasConversion(
                value => value.UtcDateTime,
                value => new DateTimeOffset(value, TimeSpan.Zero));
        });
    }

    /// <summary>
    /// Persists the unit of work, then publishes every domain event the tracked aggregates
    /// raised (as POCOs, dispatched by their runtime type) and clears them.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await base.SaveChangesAsync(cancellationToken);

        var entities = ChangeTracker.Entries<Entity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToArray();

        foreach (var entity in entities)
        {
            var domainEvents = entity.DomainEvents.ToArray();
            entity.ClearDomainEvents();

            foreach (var domainEvent in domainEvents)
            {
                await events.PublishAsync(domainEvent, cancellationToken: cancellationToken);
            }
        }

        return result;
    }
}
