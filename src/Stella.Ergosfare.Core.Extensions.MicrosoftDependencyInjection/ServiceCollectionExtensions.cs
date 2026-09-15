using Stella.Ergosfare.Core.Abstractions.Planning;
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
    /// Configures modules, validates generated plans, and registers generated participant
    /// factories before the container is built. Existing participant registrations retain
    /// their lifetime. An incompatible plan fails here, before the first dispatch.
    /// </remarks>
    public static IServiceCollection AddErgosfare(this IServiceCollection services,
        Action<IModuleRegistry> ergosfareBuilderAction)
    {
        // One catalog per container: the composition table it reads is process-wide, but
        // which of its rows this application registered is not.
        var compositions = new DispatchPlanCatalog();

        var ergosfareBuilder = new ModuleRegistry(services, compositions);
        ergosfareBuilderAction(ergosfareBuilder);
        ergosfareBuilder.Initialize();

        return services;
    }
}
