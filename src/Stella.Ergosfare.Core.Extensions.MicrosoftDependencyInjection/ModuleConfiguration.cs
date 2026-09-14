using Stella.Ergosfare.Core.Abstractions.Planning;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// What modules register into during a single <c>AddErgosfare</c> call.
/// </summary>
/// <param name="services">The container's service collection.</param>
/// <param name="compositions">This container's view of the compiled composition table.</param>
internal class ModuleConfiguration(
    IServiceCollection services, DispatchPlanCatalog compositions)
    : IModuleConfiguration
{
    /// <summary>
    /// The container's service collection.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// This container's view of the compiled composition table.
    /// </summary>
    public DispatchPlanCatalog Compositions { get; } = compositions;
}
