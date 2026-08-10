using Microsoft.Data.Sqlite;

namespace Stella.Ergosfare.E2E.Infrastructure;

/// <summary>Hands out open connections to the app's SQLite database.</summary>
public sealed class SqliteConnectionSource(string connectionString)
{
    /// <summary>Opens a connection; the caller disposes it.</summary>
    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        return connection;
    }
}
