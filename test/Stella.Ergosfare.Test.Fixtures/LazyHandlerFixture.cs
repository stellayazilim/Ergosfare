using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Test.Fixtures;

/// <summary>
/// Fixture for creating <see cref="LazyHandler{THandler, IHandlerDescriptor}"/> instances
/// with predefined descriptors and service resolution support.
/// </summary>
/// <remarks>
/// This fixture helps in testing handlers and interceptors in isolation
/// by providing a unified way to create lazy handlers with descriptors.
/// </remarks>
public class LazyHandlerFixture : IFixture<LazyHandlerFixture>
{
    // Internal fixture to create descriptors for handlers
    private readonly DescriptorFixture _descriptorFixture = new();

    /// <summary>
    /// Adds additional service configurations to the fixture's service provider.
    /// </summary>
    /// <param name="configure">An action to configure services.</param>
    /// <returns>The current <see cref="LazyHandlerFixture"/> instance for chaining.</returns>
    public LazyHandlerFixture AddServices(Action<IServiceCollection> configure) => this;

    /// <summary>
    /// Creates a <see cref="LazyHandler{THandler, IHandlerDescriptor}"/> instance
    /// for the specified handler type.
    /// </summary>
    /// <typeparam name="THandler">The type of handler to create. Must implement <see cref="IHandler"/> and have a parameterless constructor.</typeparam>
    /// <returns>A new lazy handler with its descriptor initialized.</returns>
    public LazyHandler<THandler, IHandlerDescriptor> CreateLazyHandler<THandler>()
        where THandler : IHandler, new()
    {
        // Generate a descriptor for the handler type
        var descriptor = _descriptorFixture.CreateDescriptor<THandler>();

        // Return a new LazyHandler instance with descriptor and lazy handler initialization
        return new LazyHandler<THandler, IHandlerDescriptor>
        {
            Descriptor = descriptor,
            Handler = new Lazy<THandler>(new THandler()),
        };
    }

    /// <summary>
    /// Creates a <see cref="LazyHandlerCollection{THandler, TDescriptor}"/> 
    /// containing a single lazy handler for the specified handler type.
    /// </summary>
    /// <typeparam name="THandler">The type of the handler to create. Must implement <see cref="IHandler"/> and have a parameterless constructor.</typeparam>
    /// <returns>
    /// A <see cref="ILazyHandlerCollection{THandler, IHandlerDescriptor}"/> containing exactly one lazy handler.
    /// </returns>
    public ILazyHandlerCollection<THandler, IHandlerDescriptor> CreateSingleElementLazyHandlerCollection<THandler>()
        where THandler : IHandler, new()
    {
        var lazyHandler = CreateLazyHandler<THandler>();
        return new LazyHandlerCollection<THandler, IHandlerDescriptor>([lazyHandler]);
    }
    
    
    /// <summary>
    /// Creates a <see cref="LazyHandlerCollection{THandler, TDescriptor}"/> 
    /// containing the specified lazy handlers.
    /// </summary>
    /// <param name="lazyHandlers">An array of <see cref="ILazyHandler{IHandler, IHandlerDescriptor}"/> to include in the collection.</param>
    /// <returns>
    /// A <see cref="ILazyHandlerCollection{IHandler, IHandlerDescriptor}"/> containing all provided lazy handlers.
    /// </returns>
    public ILazyHandlerCollection<THandler, IHandlerDescriptor> CreateLazyHandlerCollection<THandler>(params IEnumerable<ILazyHandler<THandler, IHandlerDescriptor>> lazyHandlers)
        where THandler : IHandler, new()
    {
        return new LazyHandlerCollection<THandler, IHandlerDescriptor>(lazyHandlers);
    }


    /// <summary>
    /// Gets the service provider used for resolving dependencies.
    /// </summary>
    public ServiceProvider ServiceProvider { get; } = null!;

    /// <summary>
    /// Provides a fresh instance of <see cref="LazyHandlerFixture"/>.
    /// </summary>
    public LazyHandlerFixture New => new();

    /// <summary>
    /// Disposes the fixture and any underlying resources.
    /// </summary>
    public void Dispose()
    {
        _descriptorFixture.Dispose();
    }
}