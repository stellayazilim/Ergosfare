// UseDefaultResultAdapter is a declaration consumed by source generation.
#pragma warning disable ERGOEXP001

using Stella.Ergosfare.Core.Abstractions;
using System.Diagnostics.CodeAnalysis;
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
        var engine = new MessageDispatchEngine(compositions);
        services.TryAddSingleton(engine);

        services.TryAddSingleton(compositions);

        RegisterParticipants();
        compositions.Seal();
    }

    private void RegisterParticipants()
    {
        var registrar = new ParticipantRegistrar(services);
        foreach (var participantType in compositions.SelectedParticipants())
        {
            if (services.Any(service => !service.IsKeyedService && service.ServiceType == participantType)) continue;
            var registration = GeneratedPlanRegistry.FindParticipantRegistration(participantType)
                ?? throw new InvalidOperationException($"Participant '{participantType}' has no generated registration. Ensure the composition root generates its closed participant types.");
            registration(registrar);
        }
    }

    private sealed class ParticipantRegistrar(IServiceCollection services) : IParticipantRegistrar
    {
        public void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TParticipant>()
            where TParticipant : class
            => services.TryAddTransient<TParticipant>();
    }
}
