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
        var planRegistry = compilation.GetTypeByMetadataName(ContractMetadataNames.PlanRegistry);

        return new ModuleBuilderAvailability(
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

}
