// The registry is the experimental result-adapter surface's own plumbing: it stores and
// registers the DefaultResultAdapter that the marked API produces.
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
/// Collects the modules an application registers and, once configuration is done, puts
/// everything they named into the container.
/// </summary>
/// <param name="services">The container being built.</param>
/// <param name="compositions">This container's view of the compiled composition table.</param>
public class ModuleRegistry(
    IServiceCollection services, FrozenCompositionCatalog compositions)
    : IModuleRegistry
{

    /// <summary>
    /// The registered modules. A set, so registering the same module twice registers it
    /// once.
    /// </summary>
    private readonly HashSet<IModule> _modules = new();

    /// <summary>
    /// Whether every participant should resolve once and be reused for the life of the
    /// process, whatever lifetime it was registered with. Set by
    /// <see cref="ModuleRegistryExtensions.ForceMemoizedHandlers"/>.
    /// </summary>
    internal bool MemoizeAllHandlers { get; set; }

    /// <summary>
    /// The configured fallback result adapter. Validated as it is configured, so a
    /// misconfigured type fails inside <c>AddErgosfare</c> rather than at some later
    /// dispatch.
    /// </summary>
    private DefaultResultAdapter? _defaultResultAdapter;

    /// <summary>
    /// Registers a module.
    /// </summary>
    /// <param name="module">The module to register.</param>
    /// <returns>The same registry, so calls can be chained.</returns>
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
    /// Builds every registered module and adds the dispatch machinery, the configured
    /// options and every selected participant to the container.
    /// </summary>
    public void Initialize()
    {
        var moduleConfiguration = new ModuleConfiguration(services, compositions);

        foreach (var module in _modules)
        {
            module.Build(moduleConfiguration);
        }
        // The factory and the pipelines it builds hold no scope and are shared across the
        // process; participant instances are resolved per dispatch from the provider the
        // dispatcher passes down. The mediator exists only to capture the scope it was
        // resolved from, and a transient does that — dependency injection hands it the
        // resolving scope's provider — without the locking and bookkeeping that opening a
        // fresh scope per dispatch would cost.
        services.TryAddSingleton<IMessageDependenciesFactory, MessageDependenciesFactory>();
        services.TryAddSingleton<PipelineExecutorCache>();
        // Registered through a factory because the engine's constructor is internal: the
        // type only means anything wired to the executor cache. A singleton, so it is built
        // once per container and every facade resolution simply receives it.
        services.TryAddSingleton(static sp => new MessageDispatchEngine(
            sp.GetRequiredService<PipelineExecutorCache>(),
            sp.GetRequiredService<IMessageDependenciesFactory>()));
        services.TryAddTransient<IMessageMediator, MessageMediator>();
        services.TryAddSingleton<RootServiceProviderAccessor>();
        services.TryAddSingleton(new ErgosfareRuntimeOptions { MemoizeAllHandlers = MemoizeAllHandlers });

        services.TryAddSingleton(compositions);

        if (_defaultResultAdapter is not null)
        {
            // An ordinary singleton: adapter binding consults it as its last tier. Without
            // one, a result type that binds nothing else keeps throwing failures rather than
            // returning them.
            services.TryAddSingleton(_defaultResultAdapter);
        }

        var allHandlerTypes = RegisterParticipants();

        // Registered as a factory so the capture happens at first resolution, once the
        // service collection is final. Taking the snapshot here would miss whatever the
        // application registers after AddErgosfare — tolerable for memoization, which would
        // only lose a fast path, but not for the plans' direct-construction check, which
        // must never claim a participant whose registration the application has since
        // overridden. Passing the participant types keeps the scan bounded to them.
        var registeredHandlerTypes = allHandlerTypes;
        services.TryAddSingleton(_ => CaptureHandlerLifetimes(services, registeredHandlerTypes));
    }


    /// <summary>
    /// Reads how each participant ended up registered, from the finalized service
    /// collection.
    /// </summary>
    /// <param name="services">The finalized service collection.</param>
    /// <param name="handlerTypes">The participant types to look for.</param>
    /// <returns>The lifetimes, and which participants are plain transient registrations.</returns>
    /// <remarks>
    /// The last registration of a type wins, which is what <c>GetRequiredService</c> would
    /// resolve. A registration counts as plain transient — the shape that lets a generated
    /// plan construct the participant itself — only when it is transient, unkeyed,
    /// registered as its own implementation, and carries no factory or instance. Keyed
    /// descriptors throw when their implementation members are read, so they are excluded
    /// before that can happen.
    /// </remarks>
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
                // A later registration that is not plain takes the type back out: what
                // matters is the one that wins.
                plainTransientRegistrations.Remove(serviceDescriptor.ServiceType);
            }
        }

        return new HandlerLifetimeRegistry(handlerLifetimes, plainTransientRegistrations);
    }

    /// <summary>
    /// Registers every participant this container both selected and can run, so pipelines
    /// can resolve them.
    /// </summary>
    /// <returns>The registered participant types, for the lifetime capture.</returns>
    /// <remarks>
    /// The participants come from the catalog, which is this container's selection narrowed
    /// to what the compiled table names as a participant — the message types registration
    /// also names are things to dispatch, not services to resolve, and never reach here.
    /// Interfaces and abstract classes are skipped because the container cannot construct
    /// them, though the composition still carries them so that dispatch through a base type
    /// works. An open generic definition is registered as it is: a composition row names the
    /// definition, and the pipeline closes it over the runtime message's arguments before
    /// resolving it.
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2072",
        Justification = "Every element originates from FrozenParticipant.HandlerType, which is annotated to " +
                        "preserve public constructors; the HashSet the catalog collects them into only " +
                        "deduplicates and cannot carry the annotation.")]
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
