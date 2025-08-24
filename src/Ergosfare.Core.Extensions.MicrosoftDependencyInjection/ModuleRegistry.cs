using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Factories;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

public class ModuleRegistry(IServiceCollection services, IMessageRegistry messageRegistry)
    : IModuleRegistry
{
    private readonly HashSet<IModule> _modules = new();

    public IModuleRegistry Register(IModule module)
    {
        _modules.Add(module);
        return this;
    }
    
    /// <summary>
    ///     Initializes the registered modules and their components.
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

        foreach (var descriptor in messageRegistry)
        {
            // Register all handler types from the registry
            RegisterHandlersFromDescriptor(descriptor);
        }
    }
    
    
    
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

    private static void CollectHandlerTypes(IEnumerable<IHandlerDescriptor> descriptors, HashSet<Type> handlerTypes)
    {
        
        
        
        foreach (var descriptor in descriptors)
        {
            handlerTypes.Add(descriptor.HandlerType);
        }
    
    }
}