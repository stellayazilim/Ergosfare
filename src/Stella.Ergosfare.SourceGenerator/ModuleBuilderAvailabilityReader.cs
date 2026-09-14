using Microsoft.CodeAnalysis;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
/// Finds out what the referenced Ergosfare packages can host.
/// </summary>
/// <param name="compilation">The consuming compilation to look in.</param>
/// <remarks>
/// Every question is asked of the compilation itself rather than of a version number, so
/// generation degrades one surface at a time: a package without staged plans gets none, and
/// an otherwise identical file.
/// </remarks>
internal sealed class ModuleBuilderAvailabilityReader(Compilation compilation)
{
    /// <summary>
    /// Asks every question once, for this compilation.
    /// </summary>
    /// <returns>What the referenced packages can host.</returns>
    internal ModuleBuilderAvailability Read()
    {
        var commandBuilder = compilation.GetTypeByMetadataName(ContractMetadataNames.CommandModuleBuilder);
        var queryBuilder = compilation.GetTypeByMetadataName(ContractMetadataNames.QueryModuleBuilder);
        var eventBuilder = compilation.GetTypeByMetadataName(ContractMetadataNames.EventModuleBuilder);
        var planRegistry = compilation.GetTypeByMetadataName(ContractMetadataNames.PlanRegistry);

        return new ModuleBuilderAvailability(
            HasCompositionCatalog: compilation.GetTypeByMetadataName(ContractMetadataNames.CompositionCatalog) is not null,
            HasCommandModuleBuilder: commandBuilder is not null,
            HasQueryModuleBuilder: queryBuilder is not null,
            HasEventModuleBuilder: eventBuilder is not null,
            CommandBuilderHasRegisterParticipants: HasRegisterParticipants(commandBuilder),
            QueryBuilderHasRegisterParticipants: HasRegisterParticipants(queryBuilder),
            EventBuilderHasRegisterParticipants: HasRegisterParticipants(eventBuilder),
            HasPlanRegistry: planRegistry is not null,
            HasKeyedServiceExtensions: compilation.GetTypeByMetadataName(ContractMetadataNames.KeyedServiceExtensions) is not null,
            PlanRegistryHasStagedPlans: planRegistry is not null
                && !planRegistry.GetMembers("AddStagedPlan").IsEmpty
                && compilation.GetTypeByMetadataName(ContractMetadataNames.ServiceProviderExtensions) is not null,
            PlanRegistryHasStreamPlans: planRegistry is not null
                && !planRegistry.GetMembers("AddStreamPlan").IsEmpty
                && compilation.GetTypeByMetadataName(ContractMetadataNames.ServiceProviderExtensions) is not null,
            HasDispatchSiteAttribute: compilation.GetTypeByMetadataName(ContractMetadataNames.DispatchSiteAttribute) is not null,
            PlanRegistryHasPipelineDescriptors: planRegistry is not null
                && !planRegistry.GetMembers("AddPipelineDescriptor").IsEmpty);
    }

    /// <summary>
    /// Reports whether a module builder takes participants in bulk.
    /// </summary>
    /// <param name="builder">The builder to check, or <c>null</c> when the module is absent.</param>
    /// <returns><c>true</c> when the bulk method is available.</returns>
    private static bool HasRegisterParticipants(INamedTypeSymbol? builder)
        => builder is not null && !builder.GetMembers("RegisterParticipants").IsEmpty;

}
