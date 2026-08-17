using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Adds the event module to a registry.
/// </summary>
public static class ModuleRegistryExtensions
{
    /// <summary>
    /// Adds the event module, registering the events <paramref name="builder"/> selects.
    /// </summary>
    /// <param name="registry">The registry being configured.</param>
    /// <param name="builder">Selects which event constructs this container runs.</param>
    /// <returns>The same registry, so calls can be chained.</returns>
    public static IModuleRegistry AddEventModule(this IModuleRegistry registry, Action<EventModuleBuilder> builder)
    {
        registry.Register(new EventModule(builder));
        return registry;
    }
}
