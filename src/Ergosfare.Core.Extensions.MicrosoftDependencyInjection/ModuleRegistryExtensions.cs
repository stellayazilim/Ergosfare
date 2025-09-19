namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Provides extension methods for configuring and registering modules with an <see cref="IModuleRegistry"/>.
/// </summary>
public static class ModuleRegistryExtensions
{
    /// <summary>
    /// Adds the core messaging module to the specified module registry.
    /// </summary>
    /// <param name="moduleRegistry">
    /// The <see cref="IModuleRegistry"/> instance to which the core messaging module will be registered.
    /// </param>
    /// <param name="builderAction">
    /// An action that configures the core messaging module using a <see cref="CoreModuleBuilder"/>.
    /// </param>
    /// <returns>
    /// The same <see cref="IModuleRegistry"/> instance, enabling fluent chaining.
    /// </returns>
    public static IModuleRegistry AddCoreModule(this IModuleRegistry moduleRegistry,
        Action<IModuleBuilder> builderAction)
    {
        moduleRegistry.Register(new CoreModule(builderAction));

        return moduleRegistry;
    }
}