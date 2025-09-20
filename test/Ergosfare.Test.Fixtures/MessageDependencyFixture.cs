using Ergosfare.Contracts.Attributes;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Abstractions.SignalHub;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ergosfare.Test.Fixtures;


// ReSharper disable once ClassNeverInstantiated.Global

/// <summary>
/// Provides a reusable fixture for testing message dependencies and handlers.
/// Implements <see cref="IFixture{TFixture}"/> for a consistent fixture API across tests.
/// </summary>
public class MessageDependencyFixture : IFixture<MessageDependencyFixture>
{
    private bool _disposed;
    private readonly Lazy<ServiceProvider> _lazyProvider;
    private readonly IServiceCollection _services;
    private readonly List<string> _groups = [GroupAttribute.DefaultGroupName];
    
    /// <summary>
    /// Gets the list of groups currently applied to this fixture.
    /// </summary>
    public IReadOnlyList<string> Groups => _groups.AsReadOnly();
    
    /// <summary>
    /// Gets the internal message registry used to track handlers.
    /// </summary>
    internal MessageRegistry Registry { get; set; }
    
    
    /// <summary>
    /// Exposes message registry
    /// </summary>
    public IMessageRegistry MessageRegistry => Registry;
    
    /// <summary>
    /// Gets the service provider built from the registered services.
    /// It is lazy to allow additional services to be registered before first use.
    /// </summary>
    public ServiceProvider ServiceProvider => _lazyProvider.Value;

    
    /// <summary>
    /// Gets a fresh, independent instance of this fixture.
    /// This allows creating a new fixture from an existing instance for per-test usage,
    /// ensuring that each test works with an isolated fixture without sharing state.
    /// </summary>
    public MessageDependencyFixture New => new ();
    
    /// <summary>
    /// Initializes a new instance of <see cref="MessageDependencyFixture"/>.
    /// Sets up the service collection and message registry.
    /// </summary>
    public MessageDependencyFixture()
    {
        _services = new ServiceCollection();
        Registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());

        _lazyProvider = new Lazy<ServiceProvider>(() => _services.BuildServiceProvider());
    }

    /// <summary>
    /// Adds one or more groups to this fixture.
    /// </summary>
    /// <param name="groups">The group names to add.</param>
    /// <returns>The current fixture instance for fluent chaining.</returns>
    public MessageDependencyFixture AddGroups(params string[] groups)
    {
        _groups.AddRange(groups);
        return this;
    }

    /// <summary>
    /// Removes one or more groups from this fixture.
    /// </summary>
    /// <param name="groups">The group names to remove.</param>
    /// <returns>The current fixture instance for fluent chaining.</returns>
    public MessageDependencyFixture RemoveGroups(params string[] groups)
    {
        _groups.RemoveAll(groups.Contains);
        return this;
    }
 
    
    /// <summary>
    /// Allows other fixtures or tests to register additional services before the service provider is built.
    /// </summary>
    /// <param name="configure">The configuration action for the service collection.</param>
    /// <returns>The current fixture instance for fluent chaining.</returns>
    public MessageDependencyFixture AddServices(Action<IServiceCollection> configure)
    {
        configure(_services);
        return this;
    }

    
    /// <summary>
    /// Registers one or more message handlers in the registry and the service collection.
    /// </summary>
    /// <param name="handlerTypes">The handler types to register.</param>
    /// <returns>The current fixture instance for fluent chaining.</returns>
    public MessageDependencyFixture RegisterHandler(params Type[] handlerTypes)
    {
        foreach (var handler in handlerTypes)
        {
            Registry.Register(handler);
            _services.TryAddTransient(handler); // allow handlers automatically registered

        }

        return this;
    }
    

    /// <summary>
    /// Creates <see cref="IMessageDependencies"/> for a generic message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type to resolve dependencies for.</typeparam>
    /// <returns>An instance of <see cref="IMessageDependencies"/>.</returns>
    public IMessageDependencies CreateDependencies(Type messageType)
    {
        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(Registry);
        var descriptor = resolver.Find(messageType);
        var factory = new MessageDependenciesFactory(ServiceProvider);
        
        return factory.Create(messageType, descriptor! , Groups);
    }
    
    /// <summary>
    /// Creates <see cref="IMessageDependencies"/> for a generic message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type to resolve dependencies for.</typeparam>
    /// <returns>An instance of <see cref="IMessageDependencies"/>.</returns>
    public IMessageDependencies CreateDependencies<TMessage>()
    {
        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(Registry);
        var descriptor = resolver.Find(typeof(TMessage));
        var factory = new MessageDependenciesFactory(ServiceProvider);
        
        return factory.Create(typeof(TMessage), descriptor ?? new MessageDescriptor(typeof(TMessage)), Groups);
    }

   
    public IMessageDependencies CreateDependenciesFromDescriptor<TMessage>(IMessageDescriptor descriptor)
    {
        var factory = new MessageDependenciesFactory(ServiceProvider);
        return factory.Create(typeof(TMessage), descriptor, Groups);
    }

    
    
    /// <summary>
    /// Disposes the fixture, including the underlying <see cref="ServiceProvider"/> and resets the <see cref="SignalHubAccessor"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        if (_lazyProvider.IsValueCreated)
            ServiceProvider.Dispose();

        SignalHubAccessor.ResetInstance();
        _disposed = true;
    }
}