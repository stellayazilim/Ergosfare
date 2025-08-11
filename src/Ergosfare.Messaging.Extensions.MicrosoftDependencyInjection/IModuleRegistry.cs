namespace Ergosfare.Messaging.Extensions.MicrosoftDependencyInjection;

public interface IModuleRegistry
{
    /// <summary>
    ///     Registers a module with the module registry.
    /// </summary>
    /// <param name="module">The module to register.</param>
    /// <returns>The instance of the module registry for method chaining.</returns>
    IModuleRegistry Register(IModule module);
}