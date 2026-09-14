// UseDefaultResultAdapter is a declaration consumed by source generation.
#pragma warning disable ERGOEXP001

using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stella.Ergosfare.Core.Abstractions.Planning;

namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Collects the modules an application registers and, once configuration is done, puts
/// everything they named into the container.
/// </summary>
/// <param name="services">The container being built.</param>
/// <param name="compositions">This container's view of the compiled composition table.</param>
public class ModuleRegistry(
    IServiceCollection services, DispatchPlanCatalog compositions)
    : IModuleRegistry
{

    /// <summary>
    /// The registered modules. A set, so registering the same module twice registers it
    /// once.
    /// </summary>
    private readonly HashSet<IModule> _modules = new();


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
    [Obsolete("Experimental API: subject to change or removal in any release.", false, DiagnosticId = ExperimentalIds.ResultAdapterSurface)]
    public IModuleRegistry UseDefaultResultAdapter(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.Interfaces)]
        Type adapterType)
    {
        ArgumentNullException.ThrowIfNull(adapterType);
        // The generator validates the type and embeds its stateless calls in plans.
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
        // The engine reads generated plans; only the facade captures the calling scope.
        services.TryAddSingleton(static sp => new MessageDispatchEngine(sp.GetRequiredService<DispatchPlanCatalog>()));

        services.TryAddSingleton(compositions);

        RegisterParticipants();
        compositions.Seal();
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
