namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Represents a registry that holds and manages modules, allowing registration and configuration.
/// </summary>
public interface IModuleRegistry
{
    /// <summary>
    /// Registers a module with the module registry.
    /// </summary>
    /// <param name="module">The module to register.</param>
    /// <returns>The instance of the module registry for method chaining.</returns>
    IModuleRegistry Register(IModule module);

    /// <summary>
    /// Configures result adapters for this module registry.
    /// </summary>
    /// <param name="builder">A delegate used to build and configure result adapters.</param>
    /// <returns>The instance of the module registry for method chaining.</returns>
    IModuleRegistry ConfigureResultAdapters(Action<ResultAdapterBuilder> builder);
}
