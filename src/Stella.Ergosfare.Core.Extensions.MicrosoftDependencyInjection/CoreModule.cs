namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;


/// <summary>
/// Represents the core module of the framework, responsible for configuring 
/// and registering core message types into the message registry.
/// </summary>
/// <remarks>
/// The <see cref="CoreModule"/> acts as an entry point for bootstrapping the system.
/// It delegates the module building process to a provided <see cref="Action{T}"/>
/// using a <see cref="CoreModuleBuilder"/>.
/// </remarks>
public sealed class CoreModule(Action<IModuleBuilder> builder): IModule
{
    
    /// <summary>
    /// Builds the module by invoking the provided builder delegate with a new
    /// <see cref="CoreModuleBuilder"/> instance configured with the current message registry.
    /// </summary>
    /// <param name="configuration">The module configuration providing access to the message registry and other options.</param>
    public void Build(IModuleConfiguration configuration)
    {
        builder(new CoreModuleBuilder(configuration.MessageRegistry));
    }
}