using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Provides extension methods for registering and configuring the Stella.Ergosfare framework
/// with the ASP.NET Core dependency injection system.
/// </summary>
public static class ServiceCollectionExtensions
{
    
    /// <summary>
    /// Adds and configures the Stella.Ergosfare framework to the application's
    /// <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> to which Stella.Ergosfare services will be added.
    /// </param>
    /// <param name="ergosfareBuilderAction">
    /// An action that configures the Stella.Ergosfare module registry using an <see cref="IModuleRegistry"/>.
    /// This allows registration of additional modules and customization of the messaging pipeline.
    /// </param>
    /// <returns>
    /// The same <see cref="IServiceCollection"/> instance, enabling fluent chaining of service registrations.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method registers:
    /// <list type="bullet">
    ///   <item><description>The dispatch machinery: the dependencies factory, executor cache and mediator.</description></item>
    ///   <item><description>A singleton <see cref="FrozenCompositionCatalog"/> — this container's view of the compiled composition table.</description></item>
    ///   <item><description>The configured default result adapter, when <see cref="IModuleRegistry.UseDefaultResultAdapter"/> was called.</description></item>
    ///   <item><description>All module-defined handlers, interceptors, and services discovered at initialization.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// After setting up dependencies, this method invokes <paramref name="ergosfareBuilderAction"/>
    /// to allow custom module configuration, then calls <see cref="ModuleRegistry.Initialize"/>
    /// to finalize the setup.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddErgosfare(this IServiceCollection services,
        Action<IModuleRegistry> ergosfareBuilderAction)
    {
        // The composition catalog is per container: the frozen table it reads is
        // process-wide, but which of its rows this application registered is not.
        var compositions = new FrozenCompositionCatalog();

        var ergosfareBuilder = new ModuleRegistry(services, compositions);
        ergosfareBuilderAction(ergosfareBuilder);
        ergosfareBuilder.Initialize();

        return services;
    }
}