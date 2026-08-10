using System.Globalization;
using Microsoft.Data.Sqlite;
using Stella.Ergosfare.E2E.Domain;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.E2E.Infrastructure;

/// <summary>
/// SQLite persistence for the one aggregate this app stores, over raw ADO.NET.
/// </summary>
/// <remarks>
/// Hand-written SQL rather than an ORM on purpose: this app doubles as the NativeAOT gate,
/// and an ORM's runtime model building and expression compilation are exactly what AOT
/// cannot do. Every read and write below is a parameterised command with explicit column
/// mapping — no reflection, nothing for the trimmer to lose.
/// </remarks>
public sealed class TodoStore(SqliteConnectionSource connections, IEventMediator events)
{
    /// <summary>
    /// Drops and recreates the schema, so the e2e assertions start from a known-empty
    /// state on every boot. <c>CreatedAt</c> is stored as round-trip UTC text, which
    /// sorts chronologically as a string.
    /// </summary>
    public async Task ResetSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            DROP TABLE IF EXISTS Todos;
            CREATE TABLE Todos (
                Id          TEXT    NOT NULL PRIMARY KEY,
                Title       TEXT    NOT NULL,
                IsCompleted INTEGER NOT NULL,
                CreatedAt   TEXT    NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Inserts a todo and publishes whatever domain events it raised.</summary>
    public async Task AddAsync(Todo todo, CancellationToken cancellationToken = default)
    {
        await using (var connection = await connections.OpenAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                INSERT INTO Todos (Id, Title, IsCompleted, CreatedAt)
                VALUES ($id, $title, $isCompleted, $createdAt);
                """;

            Bind(command, todo);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await PublishDomainEventsAsync(todo, cancellationToken);
    }

    /// <summary>Writes a todo's current state back and publishes what it raised.</summary>
    public async Task UpdateAsync(Todo todo, CancellationToken cancellationToken = default)
    {
        await using (var connection = await connections.OpenAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                UPDATE Todos
                SET Title = $title, IsCompleted = $isCompleted, CreatedAt = $createdAt
                WHERE Id = $id;
                """;

            Bind(command, todo);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await PublishDomainEventsAsync(todo, cancellationToken);
    }

    /// <summary>The todo with this id, or <c>null</c>.</summary>
    public async Task<Todo?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id, Title, IsCompleted, CreatedAt FROM Todos WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    /// <summary>Every todo, oldest first.</summary>
    public async Task<IReadOnlyList<Todo>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id, Title, IsCompleted, CreatedAt FROM Todos ORDER BY CreatedAt;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var todos = new List<Todo>();

        while (await reader.ReadAsync(cancellationToken))
        {
            todos.Add(Read(reader));
        }

        return todos;
    }

    private static void Bind(SqliteCommand command, Todo todo)
    {
        command.Parameters.AddWithValue("$id", todo.Id.ToString());
        command.Parameters.AddWithValue("$title", todo.Title);
        command.Parameters.AddWithValue("$isCompleted", todo.IsCompleted ? 1 : 0);
        command.Parameters.AddWithValue("$createdAt", todo.CreatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
    }

    private static Todo Read(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)),
        Title = reader.GetString(1),
        IsCompleted = reader.GetInt64(2) != 0,
        // RoundtripKind alone: the stored text carries its own UTC marker, and the style
        // cannot be combined with the Assume*/Adjust* ones.
        CreatedAt = new DateTimeOffset(
            DateTime.ParseExact(reader.GetString(3), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            TimeSpan.Zero),
    };

    /// <summary>
    /// The unit of work committed, so whatever the aggregate raised is now true: publish it
    /// by runtime type and clear it. This is the seam the e2e suite watches — a POCO domain
    /// event reaching an Ergosfare event handler without the Domain assembly ever
    /// referencing Ergosfare.
    /// </summary>
    private async Task PublishDomainEventsAsync(Entity entity, CancellationToken cancellationToken)
    {
        if (entity.DomainEvents.Count == 0)
        {
            return;
        }

        var domainEvents = entity.DomainEvents.ToArray();
        entity.ClearDomainEvents();

        foreach (var domainEvent in domainEvents)
        {
            await events.PublishAsync(domainEvent, cancellationToken: cancellationToken);
        }
    }
}
