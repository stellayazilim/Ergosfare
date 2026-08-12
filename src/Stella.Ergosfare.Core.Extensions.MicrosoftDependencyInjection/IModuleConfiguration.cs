using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;


/// <summary>
/// Represents the configuration context for a module.
/// Provides access to the service collection and the composition catalog
/// associated with the module during setup.
/// </summary>
public interface IModuleConfiguration
{
    /// <summary>
    ///     Gets the collection of services associated with the module configuration.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    ///     Gets this container's view of the frozen composition table — the selection
    ///     surface registration records what the application actually registered into.
    /// </summary>
    FrozenCompositionCatalog Compositions { get; }
}