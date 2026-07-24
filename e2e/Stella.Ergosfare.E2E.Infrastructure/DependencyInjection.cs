using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.E2E.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the SQLite-backed <see cref="TodoDbContext"/>.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
        => services.AddDbContext<TodoDbContext>(options => options.UseSqlite(connectionString));
}
