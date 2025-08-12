using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Context;
using Ergosfare.Core.Internal.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

public class ModuleRegistry(IServiceCollection services, IMessageRegistry messageRegistry)
    : IModuleRegistry
{
    private readonly HashSet<IModule> _modules = new();
    private readonly IServiceCollection _services = services;
    private readonly IMessageRegistry _messageRegistry = messageRegistry;


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
        var moduleConfiguration = new ModuleConfiguration(_services, _messageRegistry);

        foreach (var module in _modules)
        {
            module.Build(moduleConfiguration);
        }

        _services.TryAddTransient<IMessageMediator, MessageMediator>();
        _services.TryAddSingleton<IMessageRegistry>(_messageRegistry);
        _services.TryAddTransient(_ => AmbientExecutionContext.Current);

        foreach (var descriptor in _messageRegistry)
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
        CollectHandlerTypes(descriptor.Handler, descriptorHandlerTypes);

        // Register each type once
        foreach (var handlerType in descriptorHandlerTypes)
        {
            // Only register concrete classes with DI container - interfaces and abstract classes are kept in 
            // Ergosfare registry for polymorphic dispatch but cannot be instantiated by the DI container.
            // Without this filter, DI would throw "Cannot instantiate implementation type" errors.
            if (handlerType is { IsClass: true, IsAbstract: false })
            {
                _services.TryAddTransient(handlerType);
            }
        }
    }

    private static void CollectHandlerTypes(IHandlerDescriptor descriptor, HashSet<Type> handlerTypes)
    {
        
          handlerTypes.Add(descriptor.HandlerType);
    
    }
}