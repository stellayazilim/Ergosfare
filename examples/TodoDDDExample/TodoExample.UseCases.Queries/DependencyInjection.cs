using System.Reflection;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace TodoExample.UseCases.Queries;

public static class DependencyInjection
{
    public static IServiceCollection AddQueries(this IServiceCollection services)
    {
        return services.AddErgosfare(options => options
            .AddQueryModule( b => b
                .RegisterFromAssembly(Assembly.GetExecutingAssembly()))
        );
    }
}