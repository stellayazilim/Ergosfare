
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Internal.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;

namespace Stella.Ergosfare.Test.Fixtures;


// ReSharper disable once ClassNeverInstantiated.Global

/// <summary>
/// Provides a reusable fixture for testing message dependencies and handlers.
/// Implements <see cref="IFixture{TFixture}"/> for a consistent fixture API across tests.
/// </summary>
/// <remarks>
/// A test declares its pipeline by registering participant types; the fixture turns them
/// into the message's own composition through <see cref="FrozenCompositionBridge"/> and
/// hands it to a catalog, so the dependencies come out of the same factory a compiled
/// composition would feed. Stub participants are declared over the core contracts alone,
/// which no module marker covers and the generator therefore does not model — hence the
/// bridge rather than the compiled table.
/// </remarks>
public class MessageDependencyFixture : IFixture<MessageDependencyFixture>
{
    private bool _disposed;
    private readonly Lazy<ServiceProvider> _lazyProvider;
    private readonly IServiceCollection _services;
    private readonly List<string> _groups = [GroupAttribute.DefaultGroupName];
    private readonly List<Type> _participants = [];

    /// <summary>
    /// Gets the list of groups currently applied to this fixture.
    /// </summary>
    public IReadOnlyList<string> Groups => _groups.AsReadOnly();

    /// <summary>
    /// The participant types registered so far — the pipeline this fixture composes.
    /// </summary>
    public IReadOnlyList<Type> Participants => _participants.AsReadOnly();

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
    /// </summary>
    public MessageDependencyFixture()
    {
        _services = new ServiceCollection();

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
    /// Registers one or more pipeline participants, so they take part in the composed
    /// pipeline and resolve from the container. Message types may be passed too and are
    /// simply ignored by the composition.
    /// </summary>
    /// <param name="handlerTypes">The participant types to register.</param>
    /// <returns>The current fixture instance for fluent chaining.</returns>
    public MessageDependencyFixture RegisterHandler(params Type[] handlerTypes)
    {
        foreach (var handler in handlerTypes)
        {
            _participants.Add(handler);

            if (handler is { IsClass: true, IsAbstract: false } && !handler.IsGenericTypeDefinition)
            {
                _services.TryAddTransient(handler); // allow handlers automatically registered
            }
        }

        return this;
    }


    /// <summary>
    /// Creates <see cref="IMessageDependencies"/> for a message type from the registered
    /// participants.
    /// </summary>
    /// <param name="messageType">The message type to resolve dependencies for.</param>
    /// <returns>An instance of <see cref="IMessageDependencies"/>.</returns>
    public IMessageDependencies CreateDependencies(Type messageType)
        => CreateDependencies(FrozenCompositionBridge.FromTypes(messageType, _participants));

    /// <summary>
    /// Creates <see cref="IMessageDependencies"/> for a generic message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type to resolve dependencies for.</typeparam>
    /// <returns>An instance of <see cref="IMessageDependencies"/>.</returns>
    public IMessageDependencies CreateDependencies<TMessage>()
        => CreateDependencies(typeof(TMessage));

    /// <summary>
    /// Creates <see cref="IMessageDependencies"/> from a composition the test built itself —
    /// the seam for pipelines the participant list cannot express.
    /// </summary>
    public IMessageDependencies CreateDependenciesFromComposition(FrozenComposition composition)
        => CreateDependencies(composition);

    /// <summary>
    /// Hands the composition to a catalog as the message's own entry, so the factory
    /// resolves it exactly as it would a compiled one.
    /// </summary>
    private IMessageDependencies CreateDependencies(FrozenComposition composition)
    {
        var catalog = new FrozenCompositionCatalog();
        catalog.Add(composition);

        AddServices(services => services.TryAddSingleton(catalog));

        return new MessageDependenciesFactory(ServiceProvider).Create(composition.MessageType, Groups);
    }



    /// <summary>
    /// Disposes the fixture, including the underlying <see cref="ServiceProvider"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        if (_lazyProvider.IsValueCreated)
            ServiceProvider.Dispose();
        _disposed = true;
    }
}
