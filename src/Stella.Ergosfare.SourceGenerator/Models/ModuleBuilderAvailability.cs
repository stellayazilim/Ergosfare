namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     Which Ergosfare registration surfaces the consuming compilation references.
///     Builder extension methods are only emitted for module builders that are actually
///     reachable, so projects referencing just the abstractions still compile clean; the
///     descriptor-based registration paths are only emitted when the referenced Ergosfare
///     version exposes them, so the generator degrades gracefully to <c>Register(Type)</c>
///     emission against older packages.
/// </summary>
/// <param name="HasMessageRegistry">Whether <c>IMessageRegistry</c> is resolvable.</param>
/// <param name="HasCommandModuleBuilder">Whether the command module's DI builder is resolvable.</param>
/// <param name="HasQueryModuleBuilder">Whether the query module's DI builder is resolvable.</param>
/// <param name="HasEventModuleBuilder">Whether the event module's DI builder is resolvable.</param>
/// <param name="HasDescriptorFactory">Whether the <c>HandlerDescriptors</c> factory is resolvable.</param>
/// <param name="CommandBuilderHasRegisterDescriptors">Whether the command builder exposes <c>RegisterDescriptors</c>.</param>
/// <param name="QueryBuilderHasRegisterDescriptors">Whether the query builder exposes <c>RegisterDescriptors</c>.</param>
/// <param name="EventBuilderHasRegisterDescriptors">Whether the event builder exposes <c>RegisterDescriptors</c>.</param>
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
/// <param name="HasDescriptorCatalog">
///     Whether <c>GeneratedDescriptorCatalog</c> is resolvable — the lookup that makes
///     runtime <c>Register&lt;THandler&gt;()</c> reflection-free for generator-modeled
///     types; older packages simply skip the module-initializer emission.
/// </param>
internal readonly record struct ModuleBuilderAvailability(
    bool HasMessageRegistry,
    bool HasCommandModuleBuilder,
    bool HasQueryModuleBuilder,
    bool HasEventModuleBuilder,
    bool HasDescriptorFactory,
    bool CommandBuilderHasRegisterDescriptors,
    bool QueryBuilderHasRegisterDescriptors,
    bool EventBuilderHasRegisterDescriptors,
    bool HasDispatchRoots,
    bool DispatchRootsHasVoidPlans,
    bool DispatchRootsHasResultPlans,
    bool DispatchRootsHasPlanFactories,
    bool DispatchRootsHasProviderPlanFactories,
    bool HasKeyedServiceExtensions,
    bool HasDescriptorCatalog);
