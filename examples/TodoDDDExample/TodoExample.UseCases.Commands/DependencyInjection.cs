using System.ComponentModel.Design;

namespace TodoExample.UseCases.Commands;

public static class DependencyInjection
{
    public static IServiceContainer AddCommands(this IServiceContainer container)
    {
        return container;
    }
}