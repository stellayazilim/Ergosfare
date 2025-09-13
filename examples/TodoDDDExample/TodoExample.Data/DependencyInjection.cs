using Microsoft.Extensions.DependencyInjection;

namespace TodoExample.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddData(this IServiceCollection services)
    {
        return services.AddSingleton<TodoService>();
    }
}