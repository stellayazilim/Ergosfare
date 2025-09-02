
using Ergosfare.Core.Internal.EventHub;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddErgosfare(this IServiceCollection services,
        Action<IModuleRegistry> ergosfareBuilderAction)
    {
        
        services.TryAddTransient<HandlerDescriptorBuilderFactory>();
        // Get the singleton registry instance
        var messageRegistry = MessageRegistryAccessor.Instance;

        // Get the singleton event-hub instance
        var eventHub = EventHubAccessor.Instance;
        // Register it as a singleton in DI
        services.TryAddSingleton(messageRegistry);
        services.TryAddSingleton(eventHub);

        // Create module registry with the shared message registry
        var ergosfareBuilder = new ModuleRegistry(services, messageRegistry);
        ergosfareBuilderAction(ergosfareBuilder);
        ergosfareBuilder.Initialize();

        return services;
    }
}