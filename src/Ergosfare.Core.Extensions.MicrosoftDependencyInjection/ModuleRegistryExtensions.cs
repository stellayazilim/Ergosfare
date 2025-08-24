namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

public static class ModuleRegistryExtensions
{
    /// <summary>
    ///     Adds the core messaging module to the module registry
    /// </summary>
    /// <param name="moduleRegistry">The module registry to which the command module will be added.</param>
    /// <param name="builderAction">An action that configures the messaging module using a <see cref="CoreModuleBuilder" />.</param>
    /// <returns>The <paramref name="moduleRegistry" /> with the command module added.</returns>
    public static IModuleRegistry AddCoreModule(this IModuleRegistry moduleRegistry,
        Action<IModuleBuilder> builderAction)
    {
        moduleRegistry.Register(new CoreModule(builderAction));

        return moduleRegistry;
    }
}