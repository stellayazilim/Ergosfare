namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
///     The metadata names the generator looks up in the consumer's compilation to decide
///     what the referenced Ergosfare package can host. Emission degrades per surface rather
///     than per version number, so every one of these is a capability probe.
/// </summary>
internal static class ContractMetadataNames
{
    internal const string CompositionCatalog =
        "Stella.Ergosfare.Core.Abstractions.DispatchRoots.FrozenCompositionCatalog";

    internal const string DispatchRoots =
        "Stella.Ergosfare.Core.Abstractions.DispatchRoots.GeneratedDispatchRoots";

    internal const string StagedVoidPlan =
        "Stella.Ergosfare.Core.Abstractions.StagedPlans.StagedVoidPlan";

    internal const string CommandModuleBuilder =
        "Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder";

    internal const string QueryModuleBuilder =
        "Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection.QueryModuleBuilder";

    internal const string EventModuleBuilder =
        "Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection.EventModuleBuilder";

    internal const string DispatchSitesNamespace = "Stella.Ergosfare.Core.Abstractions.DispatchSites";

    internal const string DispatchSiteAttribute = DispatchSitesNamespace + ".DispatchSiteAttribute";

    internal const string ServiceProviderExtensions =
        "Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions";

    internal const string KeyedServiceExtensions =
        "Microsoft.Extensions.DependencyInjection.ServiceProviderKeyedServiceExtensions";
}
