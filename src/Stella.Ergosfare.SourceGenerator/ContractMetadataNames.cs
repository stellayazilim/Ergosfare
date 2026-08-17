namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
/// The types the generator looks for in a compilation to find out what the referenced
/// Ergosfare packages can host.
/// </summary>
/// <remarks>
/// Each name is a capability test rather than a version check, so generation degrades one
/// surface at a time: a package without a given type simply has nothing written against it.
/// </remarks>
internal static class ContractMetadataNames
{
    /// <summary>The per-container view of the composition table.</summary>
    internal const string CompositionCatalog =
        "Stella.Ergosfare.Core.Abstractions.DispatchRoots.FrozenCompositionCatalog";

    /// <summary>The store generated registration puts everything into.</summary>
    internal const string DispatchRoots =
        "Stella.Ergosfare.Core.Abstractions.DispatchRoots.GeneratedDispatchRoots";

    /// <summary>The base type a staged plan derives from.</summary>
    internal const string StagedVoidPlan =
        "Stella.Ergosfare.Core.Abstractions.StagedPlans.StagedVoidPlan";

    /// <summary>The builder the command module registers through.</summary>
    internal const string CommandModuleBuilder =
        "Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder";

    /// <summary>The builder the query module registers through.</summary>
    internal const string QueryModuleBuilder =
        "Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection.QueryModuleBuilder";

    /// <summary>The builder the event module registers through.</summary>
    internal const string EventModuleBuilder =
        "Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection.EventModuleBuilder";

    /// <summary>The namespace the dispatch-manifest attributes live in.</summary>
    internal const string DispatchSitesNamespace = "Stella.Ergosfare.Core.Abstractions.DispatchSites";

    /// <summary>The attribute one recorded dispatch site is written as.</summary>
    internal const string DispatchSiteAttribute = DispatchSitesNamespace + ".DispatchSiteAttribute";

    /// <summary>The extensions a generated construction resolves services through.</summary>
    internal const string ServiceProviderExtensions =
        "Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions";

    /// <summary>The extensions a generated construction resolves keyed services through.</summary>
    internal const string KeyedServiceExtensions =
        "Microsoft.Extensions.DependencyInjection.ServiceProviderKeyedServiceExtensions";
}
