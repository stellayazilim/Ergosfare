using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Represents the configuration context for a module, providing access to the service collection and the composition catalog.
/// </summary>
/// <param name="services">The service collection used for dependency injection.</param>
/// <param name="compositions">This container's view of the frozen composition table.</param>
internal class ModuleConfiguration(
    IServiceCollection services, FrozenCompositionCatalog compositions)
    : IModuleConfiguration
{
    /// <summary>
    /// Gets the service collection for registering services.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Gets this container's view of the frozen composition table.
    /// </summary>
    public FrozenCompositionCatalog Compositions { get; } = compositions;
}
