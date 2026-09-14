using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Adds the command module to a registry.
/// </summary>
public static class ModuleRegistryExtensions
{
    /// <summary>
    /// Adds the command module, registering the commands
    /// <paramref name="builderAction"/> selects.
    /// </summary>
    /// <param name="moduleRegistry">The registry being configured.</param>
    /// <param name="builderAction">Selects which command constructs this container runs.</param>
    /// <returns>The same registry, so calls can be chained.</returns>
    public static IModuleRegistry AddCommandModule(this IModuleRegistry moduleRegistry,
        Action<CommandModuleBuilder> builderAction)
    {
        moduleRegistry.Register(new CommandModule(builderAction));

        return moduleRegistry;
    }
}
