using Microsoft.CodeAnalysis;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
///     What the referenced Ergosfare package can actually host. Emission degrades surface by
///     surface rather than by version number, so every question here is a capability probe
///     against the consumer's compilation — a package without <c>AddStagedPlan</c> gets no
///     staged plans and an otherwise identical file.
/// </summary>

internal sealed class ModuleBuilderAvailabilityReader(Compilation compilation)
{
    /// <summary>Probes the compilation once, per compilation, for every surface.</summary>
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
            DispatchRootsHasProviderPlanFactories: dispatchRoots is not null
                && HasProviderFactoryOverload(dispatchRoots)
                && compilation.GetTypeByMetadataName(ContractMetadataNames.ServiceProviderExtensions) is not null,
            HasKeyedServiceExtensions: compilation.GetTypeByMetadataName(ContractMetadataNames.KeyedServiceExtensions) is not null,
            DispatchRootsHasStagedPlans: dispatchRoots is not null
                && !dispatchRoots.GetMembers("AddStagedPlan").IsEmpty
                && compilation.GetTypeByMetadataName(ContractMetadataNames.ServiceProviderExtensions) is not null,
            StagedPlansSupportDirectConstruction:
                compilation.GetTypeByMetadataName(ContractMetadataNames.StagedVoidPlan) is { } stagedVoidPlan
                && !stagedVoidPlan.GetMembers("SupportsDirectConstruction").IsEmpty,
            HasDispatchSiteAttribute: compilation.GetTypeByMetadataName(ContractMetadataNames.DispatchSiteAttribute) is not null,
            DispatchRootsHasFrozenCompositions: dispatchRoots is not null
                && !dispatchRoots.GetMembers("AddFrozenComposition").IsEmpty);
    }

    private static bool HasRegisterParticipants(INamedTypeSymbol? builder)
        => builder is not null && !builder.GetMembers("RegisterParticipants").IsEmpty;

    /// <summary>
    ///     Whether the referenced <c>GeneratedDispatchRoots</c> accepts a plan overload
    ///     with a direct-construction factory parameter — the surface the
    ///     <c>static () => new THandler()</c> emission requires.
    /// </summary>
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
    ///     Whether the referenced <c>GeneratedDispatchRoots</c> accepts a plan overload
    ///     with a provider-taking factory parameter
    ///     (<c>Func&lt;IServiceProvider, THandler&gt;</c>) — the surface the
    ///     dependency-injected construction emission requires. Recognized by delegate
    ///     arity: the parameterless factory overload's <c>Func&lt;THandler&gt;</c> has one
    ///     type argument, the provider-taking one has two.
    /// </summary>
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
