using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

public static class ModuleRegistryExtensions
{
    public static IModuleRegistry AddEventModule(this IModuleRegistry registry, Action<EventModuleBuilder> builder)
    {
        registry.Register(new EventModule(builder));
        return registry;
    }
}