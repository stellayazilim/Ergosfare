namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// Which Ergosfare surfaces the consuming compilation can actually reach.
/// </summary>
/// <param name="HasPlanRegistry">Whether the store generated registration writes into is available.</param>
/// <param name="HasKeyedServiceExtensions">
/// Whether the keyed-service extensions are available, which a construction needs when the
/// handler takes keyed parameters; without them such a handler keeps the plan form with no
/// construction.
/// </param>
/// <param name="PlanRegistryHasStagedPlans">
/// Whether the store accepts staged plans, for messages whose pipelines carry interceptors.
/// An older package simply gets none.
/// </param>
/// <param name="PlanRegistryHasStreamPlans">
/// Whether the store accepts compiled stream plans, for streaming queries. An older package
/// simply gets none.
/// </param>
/// <param name="HasDispatchSiteAttribute">
/// Whether the dispatch-manifest attributes are available. Without them no manifest is
/// written, and reachability is judged from this compilation's own dispatch sites alone.
/// </param>
/// <param name="PlanRegistryHasPipelineDescriptors">
/// Whether the store accepts compiled compositions.
/// </param>
/// <remarks>
/// Every flag is a separate capability, so generation degrades one surface at a time: a
/// project referencing only the abstractions still compiles, and an older package gets
/// whatever it can host rather than nothing.
/// </remarks>
internal readonly record struct ModuleBuilderAvailability(
    bool HasPlanRegistry,
    bool HasKeyedServiceExtensions,
    bool PlanRegistryHasStagedPlans,
    bool PlanRegistryHasStreamPlans,
    bool HasDispatchSiteAttribute,
    bool PlanRegistryHasPipelineDescriptors);
