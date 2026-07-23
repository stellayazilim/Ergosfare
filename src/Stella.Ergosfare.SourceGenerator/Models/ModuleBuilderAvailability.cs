namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     Which Ergosfare registration surfaces the consuming compilation references.
///     Builder extension methods are only emitted for module builders that are actually
///     reachable, so projects referencing just the abstractions still compile clean.
/// </summary>
/// <param name="HasMessageRegistry">Whether <c>IMessageRegistry</c> is resolvable.</param>
/// <param name="HasCommandModuleBuilder">Whether the command module's DI builder is resolvable.</param>
/// <param name="HasQueryModuleBuilder">Whether the query module's DI builder is resolvable.</param>
/// <param name="HasEventModuleBuilder">Whether the event module's DI builder is resolvable.</param>
internal readonly record struct ModuleBuilderAvailability(
    bool HasMessageRegistry,
    bool HasCommandModuleBuilder,
    bool HasQueryModuleBuilder,
    bool HasEventModuleBuilder);
