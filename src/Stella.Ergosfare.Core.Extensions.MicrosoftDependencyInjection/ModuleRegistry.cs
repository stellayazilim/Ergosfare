using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;
using Stella.Ergosfare.Core.Internal.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Represents a central registry for application modules.
/// Handles registration, initialization, and handler discovery for all modules.
/// </summary>
public class ModuleRegistry(IServiceCollection services, IMessageRegistry messageRegistry, IResultAdapterService resultAdapterService)
    : IModuleRegistry
{
    
    /// <summary>
    /// Stores the collection of registered modules.
    /// Uses a <see cref="HashSet{T}"/> to enforce uniqueness and prevent duplicates.
    /// </summary>
    private readonly HashSet<IModule> _modules = new();

    
    /// <summary>
    /// Registers a module with the registry.
    /// </summary>
    /// <param name="module">The module to register.</param>
    /// <returns>The current <see cref="IModuleRegistry"/> instance for fluent chaining.</returns>
    public IModuleRegistry Register(IModule module)
    {
        _modules.Add(module);
        return this;
    }
    
    /// <summary>
    /// Initializes all registered modules, sets up their configurations,
    /// and registers all required handlers and services with the DI container.
    /// </summary>
    public void Initialize()
    {
        var moduleConfiguration = new ModuleConfiguration(services, messageRegistry);

        foreach (var module in _modules)
        {
            module.Build(moduleConfiguration);
        }
        services.TryAddTransient<IMessageDependenciesFactory, MessageDependenciesFactory>();
        services.TryAddTransient<IMessageMediator, MessageMediator>();
        services.TryAddSingleton(messageRegistry);
        services.TryAddTransient(_ => AmbientExecutionContext.Current);
        services.TryAddTransient<ActualTypeOrFirstAssignableTypeMessageResolveStrategy>();
        foreach (var descriptor in messageRegistry)
        {
            // Register all handler types from the registry
            RegisterHandlersFromDescriptor(descriptor);
        }
    }
    
    
    /// <summary>
    /// Collects and registers all handler types defined in the given message descriptor.
    /// </summary>
    /// <param name="descriptor">The message descriptor containing handler metadata.</param>
    private void RegisterHandlersFromDescriptor(IMessageDescriptor descriptor)
    {
        // Use a local HashSet to avoid redundant registrations within the same descriptor
        var descriptorHandlerTypes = new HashSet<Type>();

        // Process all handlers first to avoid redundant service registrations
        CollectHandlerTypes(descriptor.Handlers, descriptorHandlerTypes);
        CollectHandlerTypes(descriptor.IndirectHandlers, descriptorHandlerTypes);
        CollectHandlerTypes(descriptor.PreInterceptors, descriptorHandlerTypes);
        CollectHandlerTypes(descriptor.IndirectPreInterceptors, descriptorHandlerTypes);
        CollectHandlerTypes(descriptor.PostInterceptors, descriptorHandlerTypes);
        CollectHandlerTypes(descriptor.IndirectPostInterceptors, descriptorHandlerTypes);
        CollectHandlerTypes(descriptor.ExceptionInterceptors, descriptorHandlerTypes);
        CollectHandlerTypes(descriptor.IndirectExceptionInterceptors, descriptorHandlerTypes);

        // Register each type once
        foreach (var handlerType in descriptorHandlerTypes)
        {
            // Only register concrete classes with DI container - interfaces and abstract classes are kept in 
            // LiteBus registry for polymorphic dispatch but cannot be instantiated by the DI container.
            // Without this filter, DI would throw "Cannot instantiate implementation type" errors.
            if (handlerType is { IsClass: true, IsAbstract: false })
            {
                services.TryAddTransient(handlerType);
            }
        }
    }

    /// <summary>
    /// Configures the result adapter pipeline using the provided builder action.
    /// </summary>
    /// <param name="builder">
    /// An action that configures the <see cref="ResultAdapterBuilder"/> 
    /// with custom adapters.
    /// </param>
    /// <returns>The current <see cref="IModuleRegistry"/> instance for fluent chaining.</returns>
    public IModuleRegistry ConfigureResultAdapters(Action<ResultAdapterBuilder> builder)
    {
        builder(new ResultAdapterBuilder(resultAdapterService));
        return this;
    }

    
    /// <summary>
    /// Adds all handler types from the specified descriptors into the given set.
    /// </summary>
    /// <param name="descriptors">A collection of handler descriptors.</param>
    /// <param name="handlerTypes">The set into which handler types are added.</param>
    private static void CollectHandlerTypes(IEnumerable<IHandlerDescriptor> descriptors, HashSet<Type> handlerTypes)
    {
        foreach (var descriptor in descriptors)
        {
            handlerTypes.Add(descriptor.HandlerType);
        }
    
    }
}