using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;


/// <summary>
/// What a module registers into while the container is being built.
/// </summary>
public interface IModuleConfiguration
{
    /// <summary>
    /// The container's service collection.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// This container's view of the compiled composition table, where a module records the
    /// participants it registered.
    /// </summary>
    FrozenCompositionCatalog Compositions { get; }
}
