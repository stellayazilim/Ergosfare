using System.Reflection;
using Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace TodosExample.UseCases.Commands;

public static class DependencyInjection
{
    public static IServiceCollection AddCommands(this IServiceCollection services)
    {
        return services.AddErgosfare(o =>
        {
            o.AddCommandModule(b => b
                .RegisterFromAssembly(Assembly.GetExecutingAssembly())
            );
        });
    }
}