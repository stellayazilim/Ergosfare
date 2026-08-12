using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.E2E.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the SQLite-backed <see cref="TodoStore"/>.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
        => services
            .AddSingleton(new SqliteConnectionSource(connectionString))
            .AddScoped<TodoStore>();
}
