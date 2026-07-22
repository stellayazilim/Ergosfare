using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;
using Stella.Ergosfare.Core.Internal.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stella.Ergosfare.Core.Abstractions.Caching;
using Stella.Ergosfare.Core.Internal;
using Stella.Ergosfare.Core.Internal.Caching;
using Stella.Ergosfare.Core.Internal.Registry;

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
    /// When set (via <c>ForceMemoizedHandlers()</c>), every handler graph is memoized
    /// process-wide regardless of registered DI lifetimes — the pre-v1.2 behavior.
    /// </summary>
    internal bool MemoizeAllHandlers { get; set; }

    
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
        // The factory and its dependency graphs are provider-independent and cached
        // process-wide; handler instances resolve per invocation from the execution
        // context's provider. The mediator stays scoped only to capture the calling
        // scope's provider into that context — it is a thin, cheap wrapper.
        services.TryAddSingleton<IMessageDependenciesFactory, MessageDependenciesFactory>();
        services.TryAddScoped<IMessageMediator, MessageMediator>();
        services.TryAddSingleton<IDescriptorCacheStrategy, LruCacheStrategy>();
        services.TryAddSingleton<MessageDescriptorCache>();
        services.TryAddSingleton<RootServiceProviderAccessor>();
        services.TryAddSingleton(new ErgosfareRuntimeOptions { MemoizeAllHandlers = MemoizeAllHandlers });

        services.TryAddSingleton(messageRegistry);
        services.TryAddSingleton<ActualTypeOrFirstAssignableTypeMessageResolveStrategy>();

        var allHandlerTypes = new HashSet<Type>();
        foreach (var descriptor in messageRegistry)
        {
            // Register all handler types from the registry
            RegisterHandlersFromDescriptor(descriptor, allHandlerTypes);
        }

        // Capture the effective DI lifetime of every handler type (a user registration made
        // before AddErgosfare wins over the TryAddTransient defaults above). Messages whose
        // whole pipeline is singleton-registered keep the memoized fast path; the rest are
        // resolved per scope so scoped/transient dependencies are honored.
        var handlerLifetimes = new Dictionary<Type, ServiceLifetime>();
        foreach (var serviceDescriptor in services)
        {
            if (allHandlerTypes.Contains(serviceDescriptor.ServiceType))
            {
                handlerLifetimes[serviceDescriptor.ServiceType] = serviceDescriptor.Lifetime;
            }
        }

        services.TryAddSingleton(new HandlerLifetimeRegistry(handlerLifetimes));
    }
    
    
    /// <summary>
    /// Collects and registers all handler types defined in the given message descriptor.
    /// </summary>
    /// <param name="descriptor">The message descriptor containing handler metadata.</param>
    /// <param name="allHandlerTypes">Accumulates every registered handler type across descriptors.</param>
    private void RegisterHandlersFromDescriptor(IMessageDescriptor descriptor, HashSet<Type> allHandlerTypes)
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
        CollectHandlerTypes(descriptor.FinalInterceptors, descriptorHandlerTypes);
        CollectHandlerTypes(descriptor.IndirectFinalInterceptors, descriptorHandlerTypes);

        // Register each type once
        foreach (var handlerType in descriptorHandlerTypes)
        {
            // Only register concrete classes with DI container - interfaces and abstract classes are kept in
            // the Ergosfare registry for polymorphic dispatch but cannot be instantiated by the DI container.
            // Without this filter, DI would throw "Cannot instantiate implementation type" errors.
            if (handlerType is { IsClass: true, IsAbstract: false })
            {
                services.TryAddTransient(handlerType);
                allHandlerTypes.Add(handlerType);
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