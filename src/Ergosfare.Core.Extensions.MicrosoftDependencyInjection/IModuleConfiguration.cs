using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection;


/// <summary>
/// Represents the configuration context for a module.
/// Provides access to the service collection and message registry
/// associated with the module during setup.
/// </summary>
public interface IModuleConfiguration
{
    /// <summary>
    ///     Gets the collection of services associated with the module configuration.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    ///     Gets the message registry associated with the module configuration.
    /// </summary>
    IMessageRegistry MessageRegistry { get; }
    
    
}