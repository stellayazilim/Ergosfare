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
        var dispatchRoots = compilation.GetTypeByMetadataName(ContractMetadataNames.DispatchRoots);

        return new ModuleBuilderAvailability(
            HasCompositionCatalog: compilation.GetTypeByMetadataName(ContractMetadataNames.CompositionCatalog) is not null,
            HasCommandModuleBuilder: commandBuilder is not null,
            HasQueryModuleBuilder: queryBuilder is not null,
            HasEventModuleBuilder: eventBuilder is not null,
            CommandBuilderHasRegisterParticipants: HasRegisterParticipants(commandBuilder),
            QueryBuilderHasRegisterParticipants: HasRegisterParticipants(queryBuilder),
            EventBuilderHasRegisterParticipants: HasRegisterParticipants(eventBuilder),
            HasDispatchRoots: dispatchRoots is not null,
            DispatchRootsHasVoidPlans: dispatchRoots is not null && !dispatchRoots.GetMembers("AddVoidPlan").IsEmpty,
            DispatchRootsHasResultPlans: dispatchRoots is not null && !dispatchRoots.GetMembers("AddResultPlan").IsEmpty,
            DispatchRootsHasPlanFactories: dispatchRoots is not null && HasFactoryOverload(dispatchRoots),
            // Writing a construction also needs the extensions it resolves dependencies
            // through, so both have to be present.
            DispatchRootsHasProviderPlanFactories: dispatchRoots is not null
                && HasProviderFactoryOverload(dispatchRoots)
                && compilation.GetTypeByMetadataName(ContractMetadataNames.ServiceProviderExtensions) is not null,
            HasKeyedServiceExtensions: compilation.GetTypeByMetadataName(ContractMetadataNames.KeyedServiceExtensions) is not null,
            DispatchRootsHasStagedPlans: dispatchRoots is not null
                && !dispatchRoots.GetMembers("AddStagedPlan").IsEmpty
                && compilation.GetTypeByMetadataName(ContractMetadataNames.ServiceProviderExtensions) is not null,
            DispatchRootsHasStreamPlans: dispatchRoots is not null
                && !dispatchRoots.GetMembers("AddStreamPlan").IsEmpty
                && compilation.GetTypeByMetadataName(ContractMetadataNames.ServiceProviderExtensions) is not null,
            StagedPlansSupportDirectConstruction:
                compilation.GetTypeByMetadataName(ContractMetadataNames.StagedVoidPlan) is { } stagedVoidPlan
                && !stagedVoidPlan.GetMembers("SupportsDirectConstruction").IsEmpty,
            HasDispatchSiteAttribute: compilation.GetTypeByMetadataName(ContractMetadataNames.DispatchSiteAttribute) is not null,
            DispatchRootsHasFrozenCompositions: dispatchRoots is not null
                && !dispatchRoots.GetMembers("AddFrozenComposition").IsEmpty);
    }

    /// <summary>
    /// Reports whether a module builder takes participants in bulk.
    /// </summary>
    /// <param name="builder">The builder to check, or <c>null</c> when the module is absent.</param>
    /// <returns><c>true</c> when the bulk method is available.</returns>
    private static bool HasRegisterParticipants(INamedTypeSymbol? builder)
        => builder is not null && !builder.GetMembers("RegisterParticipants").IsEmpty;

    /// <summary>
    /// Reports whether plans may carry a way to construct the handler.
    /// </summary>
    /// <param name="dispatchRoots">The store generated registration writes into.</param>
    /// <returns><c>true</c> when an overload taking a construction exists.</returns>
    private static bool HasFactoryOverload(INamedTypeSymbol dispatchRoots)
    {
        foreach (var member in dispatchRoots.GetMembers("AddVoidPlan"))
        {
            if (member is IMethodSymbol { Parameters.Length: 1 })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reports whether plans may carry a construction that resolves the handler's
    /// dependencies from a provider.
    /// </summary>
    /// <param name="dispatchRoots">The store generated registration writes into.</param>
    /// <returns><c>true</c> when the provider-taking overload exists.</returns>
    /// <remarks>
    /// Told apart from the plain overload by how many type arguments the delegate takes: the
    /// plain construction has one, the provider-taking one has two.
    /// </remarks>
    private static bool HasProviderFactoryOverload(INamedTypeSymbol dispatchRoots)
    {
        foreach (var member in dispatchRoots.GetMembers("AddVoidPlan"))
        {
            if (member is IMethodSymbol { Parameters.Length: 1 } method
                && method.Parameters[0].Type is INamedTypeSymbol { Arity: 2 })
            {
                return true;
            }
        }

        return false;
    }
}
