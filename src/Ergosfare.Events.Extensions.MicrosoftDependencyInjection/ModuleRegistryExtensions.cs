using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Provides extension methods for registering the event module in an <see cref="IModuleRegistry"/>.
/// </summary>
public static class ModuleRegistryExtensions
{
    
    /// <summary>
    /// Adds the <see cref="EventModule"/> to the module registry.
    /// </summary>
    /// <param name="registry">The module registry to which the event module will be added.</param>
    /// <param name="builder">
    /// An action to configure the <see cref="EventModuleBuilder"/>, 
    /// allowing registration of events in the message registry.
    /// </param>
    /// <returns>The same <see cref="IModuleRegistry"/> instance for fluent chaining.</returns>
    public static IModuleRegistry AddEventModule(this IModuleRegistry registry, Action<EventModuleBuilder> builder)
    {
        registry.Register(new EventModule(builder));
        return registry;
    }
}