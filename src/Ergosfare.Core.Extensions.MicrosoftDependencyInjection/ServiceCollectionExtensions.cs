using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Core.Abstractions.SignalHub;
using Ergosfare.Core.Internal.Contexts;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Provides extension methods for registering and configuring the Ergosfare framework
/// with the ASP.NET Core dependency injection system.
/// </summary>
public static class ServiceCollectionExtensions
{
    
    /// <summary>
    /// Adds and configures the Ergosfare framework to the application's
    /// <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> to which Ergosfare services will be added.
    /// </param>
    /// <param name="ergosfareBuilderAction">
    /// An action that configures the Ergosfare module registry using an <see cref="IModuleRegistry"/>.
    /// This allows registration of additional modules and customization of the messaging pipeline.
    /// </param>
    /// <returns>
    /// The same <see cref="IServiceCollection"/> instance, enabling fluent chaining of service registrations.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method registers:
    /// <list type="bullet">
    ///   <item><description>A singleton <see cref="IResultAdapterService"/> for result adaptation.</description></item>
    ///   <item><description>Transient factories and descriptor builders for message handling.</description></item>
    ///   <item><description>A singleton <see cref="IMessageRegistry"/> for message type discovery.</description></item>
    ///   <item><description>A singleton event hub for publishing and subscribing to signals.</description></item>
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
        var resultAdapterService = new ResultAdapterService();
        services.TryAddSingleton<IResultAdapterService>(resultAdapterService);
        services.TryAddTransient<HandlerDescriptorBuilderFactory>();
        // Get the singleton registry instance
        var messageRegistry = MessageRegistryAccessor.Instance;

        // Get the singleton event-hub instance
        var eventHub = SignalHubAccessor.Instance;
        // Register it as a singleton in DI
        services.TryAddSingleton(messageRegistry);
        services.TryAddSingleton(eventHub);

        // Create module registry with the shared message registry
        var ergosfareBuilder = new ModuleRegistry(services, messageRegistry, resultAdapterService);
        ergosfareBuilderAction(ergosfareBuilder);
        ergosfareBuilder.Initialize();

        return services;
    }
}