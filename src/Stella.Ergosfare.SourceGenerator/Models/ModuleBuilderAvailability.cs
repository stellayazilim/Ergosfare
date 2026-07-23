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
internal readonly record struct ModuleBuilderAvailability(
    bool HasMessageRegistry,
    bool HasCommandModuleBuilder,
    bool HasQueryModuleBuilder,
    bool HasEventModuleBuilder,
    bool HasDescriptorFactory,
    bool CommandBuilderHasRegisterDescriptors,
    bool QueryBuilderHasRegisterDescriptors,
    bool EventBuilderHasRegisterDescriptors,
    bool HasDispatchRoots);
