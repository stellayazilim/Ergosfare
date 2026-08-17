using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Adds Ergosfare to a dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{

    /// <summary>
    /// Registers Ergosfare and the modules configured in
    /// <paramref name="ergosfareBuilderAction"/>.
    /// </summary>
    /// <param name="services">The container to add Ergosfare to.</param>
    /// <param name="ergosfareBuilderAction">
    /// Configures the registry: which modules to register, and how the framework should
    /// behave.
    /// </param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// This registers the dispatch machinery — the dependencies factory, the pipeline
    /// executors and the mediators — along with this container's view of the compiled
    /// composition table, the default result adapter if one was configured, and every
    /// participant the registered modules named.
    /// </para>
    /// <para>
    /// The modules are configured first and the registry is finalized afterwards, so
    /// everything registered during configuration is in place before the container is built.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddErgosfare(this IServiceCollection services,
        Action<IModuleRegistry> ergosfareBuilderAction)
    {
        // One catalog per container: the composition table it reads is process-wide, but
        // which of its rows this application registered is not.
        var compositions = new FrozenCompositionCatalog();

        var ergosfareBuilder = new ModuleRegistry(services, compositions);
        ergosfareBuilderAction(ergosfareBuilder);
        ergosfareBuilder.Initialize();

        return services;
    }
}
