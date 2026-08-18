namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// Which Ergosfare surfaces the consuming compilation can actually reach.
/// </summary>
/// <param name="HasCompositionCatalog">Whether the per-container composition catalog is available.</param>
/// <param name="HasCommandModuleBuilder">Whether the command module's builder is available.</param>
/// <param name="HasQueryModuleBuilder">Whether the query module's builder is available.</param>
/// <param name="HasEventModuleBuilder">Whether the event module's builder is available.</param>
/// <param name="CommandBuilderHasRegisterParticipants">Whether the command builder takes participants in bulk.</param>
/// <param name="QueryBuilderHasRegisterParticipants">Whether the query builder takes participants in bulk.</param>
/// <param name="EventBuilderHasRegisterParticipants">Whether the event builder takes participants in bulk.</param>
/// <param name="HasDispatchRoots">Whether the store generated registration writes into is available.</param>
/// <param name="DispatchRootsHasVoidPlans">Whether the store accepts plans for resultless messages.</param>
/// <param name="DispatchRootsHasResultPlans">Whether the store accepts plans for result-producing messages.</param>
/// <param name="DispatchRootsHasPlanFactories">
/// Whether plans may carry a way to construct the handler. An older package takes only the
/// plain form, and generation falls back to it.
/// </param>
/// <param name="DispatchRootsHasProviderPlanFactories">
/// Whether plans may carry a construction that resolves the handler's dependencies from a
/// provider, and the compilation can name the extensions such a construction needs. Without
/// both, a handler with dependencies keeps the plan form that has no construction at all.
/// </param>
/// <param name="HasKeyedServiceExtensions">
/// Whether the keyed-service extensions are available, which a construction needs when the
/// handler takes keyed parameters; without them such a handler keeps the plan form with no
/// construction.
/// </param>
/// <param name="DispatchRootsHasStagedPlans">
/// Whether the store accepts staged plans, for messages whose pipelines carry interceptors.
/// An older package simply gets none.
/// </param>
/// <param name="DispatchRootsHasStreamPlans">
/// Whether the store accepts compiled stream plans, for streaming queries. An older package
/// simply gets none.
/// </param>
/// <param name="StagedPlansSupportDirectConstruction">
/// Whether staged plans can construct participants themselves. Against an older package the
/// direct variant is not written and plans resolve through the provider.
/// </param>
/// <param name="HasDispatchSiteAttribute">
/// Whether the dispatch-manifest attributes are available. Without them no manifest is
/// written, and reachability is judged from this compilation's own dispatch sites alone.
/// </param>
/// <param name="DispatchRootsHasFrozenCompositions">
/// Whether the store accepts compiled compositions.
/// </param>
/// <remarks>
/// Every flag is a separate capability, so generation degrades one surface at a time: a
/// project referencing only the abstractions still compiles, and an older package gets
/// whatever it can host rather than nothing.
/// </remarks>
internal readonly record struct ModuleBuilderAvailability(
    bool HasCompositionCatalog,
    bool HasCommandModuleBuilder,
    bool HasQueryModuleBuilder,
    bool HasEventModuleBuilder,
    bool CommandBuilderHasRegisterParticipants,
    bool QueryBuilderHasRegisterParticipants,
    bool EventBuilderHasRegisterParticipants,
    bool HasDispatchRoots,
    bool DispatchRootsHasVoidPlans,
    bool DispatchRootsHasResultPlans,
    bool DispatchRootsHasPlanFactories,
    bool DispatchRootsHasProviderPlanFactories,
    bool HasKeyedServiceExtensions,
    bool DispatchRootsHasStagedPlans,
    bool DispatchRootsHasStreamPlans,
    bool StagedPlansSupportDirectConstruction,
    bool HasDispatchSiteAttribute,
    bool DispatchRootsHasFrozenCompositions);
