// The registry is the experimental result-adapter surface's own plumbing: it stores and
// registers the DefaultResultAdapter the marked API produces.
#pragma warning disable ERGOEXP001

using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Results;
using Stella.Ergosfare.Core.Internal.Factories;
using Stella.Ergosfare.Core.Internal.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Internal;
using Stella.Ergosfare.Core.Internal.Registry;

namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Represents a central registry for application modules.
/// Handles registration, initialization, and handler discovery for all modules.
/// </summary>
public class ModuleRegistry(
    IServiceCollection services, FrozenCompositionCatalog compositions)
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
    /// The configured default result adapter, validated eagerly so a misconfigured type
    /// fails inside <c>AddErgosfare</c> rather than on some first dispatch.
    /// </summary>
    private DefaultResultAdapter? _defaultResultAdapter;

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

    /// <inheritdoc />
    [Experimental(ExperimentalIds.ResultAdapterSurface)]
    public IModuleRegistry UseDefaultResultAdapter(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.Interfaces)]
        Type adapterType)
    {
        _defaultResultAdapter = new DefaultResultAdapter(adapterType);
        return this;
    }

    /// <summary>
    /// Initializes all registered modules, sets up their configurations,
    /// and registers all required handlers and services with the DI container.
    /// </summary>
    public void Initialize()
    {
        var moduleConfiguration = new ModuleConfiguration(services, compositions);

        foreach (var module in _modules)
        {
            module.Build(moduleConfiguration);
        }
        // The factory and its dependency graphs are provider-independent and cached
        // process-wide; handler instances resolve per invocation from the execution
        // context's provider. The mediator exists only to capture the calling scope's
        // provider — a transient does that too (DI hands it the resolving scope's
        // provider), without the scoped-resolution lock and resolved-services dictionary
        // insert that a fresh scope per dispatch would pay on every single dispatch.
        services.TryAddSingleton<IMessageDependenciesFactory, MessageDependenciesFactory>();
        services.TryAddSingleton<PipelineExecutorCache>();
        // Factory registration because the engine's constructor is internal (the type is
        // only meaningful wired to the executor cache); singleton, so the cost is paid once
        // per container while every facade resolution ctor-injects it as a constant.
        services.TryAddSingleton(static sp => new MessageDispatchEngine(
            sp.GetRequiredService<PipelineExecutorCache>(),
            sp.GetRequiredService<IMessageDependenciesFactory>()));
        services.TryAddTransient<IMessageMediator, MessageMediator>();
        services.TryAddSingleton<RootServiceProviderAccessor>();
        services.TryAddSingleton(new ErgosfareRuntimeOptions { MemoizeAllHandlers = MemoizeAllHandlers });

        services.TryAddSingleton(compositions);

        if (_defaultResultAdapter is not null)
        {
            // A normal singleton service: the binding's provider-taking overload consults
            // it as the last resolution tier. Absent, every unannotated foreign slot keeps
            // the classic try/catch semantics.
            services.TryAddSingleton(_defaultResultAdapter);
        }

        var allHandlerTypes = RegisterParticipants();

        // The lifetime registry is registered as a FACTORY so the capture runs at first
        // resolution — after BuildServiceProvider, when the collection is final. A snapshot
        // taken here (inside AddErgosfare) would miss registrations the user adds
        // afterwards; for memoization that staleness only cost the fast path, but the
        // plans' direct-construction gate must never claim a handler whose effective
        // registration the user has overridden. The captured set of handler types keeps
        // the scan bounded to pipeline participants.
        var registeredHandlerTypes = allHandlerTypes;
        services.TryAddSingleton(_ => CaptureHandlerLifetimes(services, registeredHandlerTypes));
    }
    
    
    /// <summary>
    /// Builds the lifetime registry from the finalized service collection: the effective
    /// DI lifetime of every handler type (last registration wins, mirroring
    /// <c>GetRequiredService</c>), and the subset whose effective registration is a plain
    /// transient self-registration — the shape the module's own <c>TryAddTransient</c>
    /// produces, and the only shape for which a generated plan may construct the handler
    /// directly. Keyed descriptors throw on the implementation members, so they are
    /// excluded up front.
    /// </summary>
    private static HandlerLifetimeRegistry CaptureHandlerLifetimes(
        IServiceCollection services, HashSet<Type> handlerTypes)
    {
        var handlerLifetimes = new Dictionary<Type, ServiceLifetime>();
        var plainTransientRegistrations = new HashSet<Type>();

        foreach (var serviceDescriptor in services)
        {
            if (!handlerTypes.Contains(serviceDescriptor.ServiceType))
            {
                continue;
            }

            handlerLifetimes[serviceDescriptor.ServiceType] = serviceDescriptor.Lifetime;

            var isPlain = !serviceDescriptor.IsKeyedService
                          && serviceDescriptor.Lifetime == ServiceLifetime.Transient
                          && serviceDescriptor.ImplementationType == serviceDescriptor.ServiceType
                          && serviceDescriptor.ImplementationFactory is null
                          && serviceDescriptor.ImplementationInstance is null;

            if (isPlain)
            {
                plainTransientRegistrations.Add(serviceDescriptor.ServiceType);
            }
            else
            {
                plainTransientRegistrations.Remove(serviceDescriptor.ServiceType);
            }
        }

        return new HandlerLifetimeRegistry(handlerLifetimes, plainTransientRegistrations);
    }

    /// <summary>
    /// Registers every pipeline participant this container both selected and can run, so
    /// the pipeline can resolve it, and returns the set for the lifetime capture.
    /// </summary>
    /// <remarks>
    /// The participants come from the catalog — the container's own selection narrowed to
    /// what the compiled table actually names as a participant. The message types
    /// registration also names are constructs to dispatch, not services to resolve, and
    /// never reach this. Interfaces and abstract classes are skipped for the reason they
    /// always were: the container cannot instantiate them, while the composition still
    /// carries them for polymorphic dispatch. Open generic definitions are registered as
    /// they are — a row names the definition, and the pipeline closes it over the runtime
    /// message's arguments before resolving it.
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2072",
        Justification = "Every element originates from a frozen participant row, whose handler type the generated " +
                        "table references through typeof — statically rooted, so the constructors survive trimming.")]
    private HashSet<Type> RegisterParticipants()
    {
        var registered = new HashSet<Type>();

        foreach (var participantType in compositions.SelectedParticipants())
        {
            if (participantType is not { IsClass: true, IsAbstract: false })
            {
                continue;
            }

            services.TryAddTransient(participantType);
            registered.Add(participantType);
        }

        return registered;
    }
}