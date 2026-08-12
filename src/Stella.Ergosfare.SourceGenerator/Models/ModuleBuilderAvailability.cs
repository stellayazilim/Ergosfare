namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     Which Ergosfare registration surfaces the consuming compilation references.
///     Builder extension methods are only emitted for module builders that are actually
///     reachable, so projects referencing just the abstractions still compile clean; the
///     bulk participant path is only emitted when the referenced Ergosfare version exposes
///     it, so the generator degrades gracefully to per-type <c>Register(Type)</c> emission
///     against older packages.
/// </summary>
/// <param name="HasCompositionCatalog">Whether <c>FrozenCompositionCatalog</c> is resolvable.</param>
/// <param name="HasCommandModuleBuilder">Whether the command module's DI builder is resolvable.</param>
/// <param name="HasQueryModuleBuilder">Whether the query module's DI builder is resolvable.</param>
/// <param name="HasEventModuleBuilder">Whether the event module's DI builder is resolvable.</param>
/// <param name="CommandBuilderHasRegisterParticipants">Whether the command builder exposes <c>RegisterParticipants</c>.</param>
/// <param name="QueryBuilderHasRegisterParticipants">Whether the query builder exposes <c>RegisterParticipants</c>.</param>
/// <param name="EventBuilderHasRegisterParticipants">Whether the event builder exposes <c>RegisterParticipants</c>.</param>
/// <param name="HasDispatchRoots">Whether the <c>GeneratedDispatchRoots</c> store is resolvable.</param>
/// <param name="DispatchRootsHasVoidPlans">Whether the store exposes <c>AddVoidPlan</c> (compile-time pipeline plans).</param>
/// <param name="DispatchRootsHasResultPlans">Whether the store exposes <c>AddResultPlan</c> (result pipeline plans).</param>
/// <param name="DispatchRootsHasPlanFactories">
///     Whether the plan surface accepts direct-construction factories (the
///     <c>Func&lt;THandler&gt;</c> overloads); older packages take only the parameterless
///     form, and emission degrades accordingly.
/// </param>
/// <param name="DispatchRootsHasProviderPlanFactories">
///     Whether the plan surface accepts provider-taking construction factories (the
///     <c>Func&lt;IServiceProvider, THandler&gt;</c> overloads) and the compilation can
///     name <c>ServiceProviderServiceExtensions</c> the emitted factory resolves
///     dependencies through; without either, dependency-injected handlers keep the
///     factory-less plan form.
/// </param>
/// <param name="HasKeyedServiceExtensions">
///     Whether <c>ServiceProviderKeyedServiceExtensions</c> is resolvable — required by
///     factories for handlers with <c>[FromKeyedServices]</c> parameters; without it such
///     handlers keep the factory-less plan form.
/// </param>
/// <param name="DispatchRootsHasStagedPlans">
///     Whether the store exposes <c>AddStagedPlan</c> (staged pipeline plans for
///     interceptor-bearing messages); older packages simply skip the staged emission.
/// </param>
/// <param name="StagedPlansSupportDirectConstruction">
///     Whether the staged plan bases expose the direct-construction surface
///     (<c>SupportsDirectConstruction</c>/<c>ExecuteDirect</c>); against older packages
///     the emission skips the direct variant and plans resolve through the provider.
/// </param>
/// <param name="HasDispatchSiteAttribute">
///     Whether <c>DispatchSiteAttribute</c> is resolvable — the dispatch-manifest surface;
///     against older packages the manifest emission is skipped and closure judgments
///     degrade to the current compilation's own sites.
/// </param>
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
    bool StagedPlansSupportDirectConstruction,
    bool HasDispatchSiteAttribute,
    bool DispatchRootsHasFrozenCompositions);
