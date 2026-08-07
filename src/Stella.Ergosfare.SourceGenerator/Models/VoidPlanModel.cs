namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     A compile-time void pipeline plan: a dispatchable command message whose entire
///     discovered pipeline is a single default-discovery, default-group async handler.
///     Emitted as <c>GeneratedDispatchRoots.AddVoidPlan&lt;TMessage, THandler&gt;()</c> so
///     the runtime executor closes over both types and calls the handler devirtualized.
///     The runtime re-validates the pipeline per registry version, so a plan that turns
///     out not to match the actual registrations only loses the speedup, never behavior.
/// </summary>
/// <param name="MessageTypeExpression">Fully qualified expression of the closed message type.</param>
/// <param name="HandlerTypeExpression">Fully qualified expression of the sole handler type.</param>
/// <param name="HasDirectConstruction">
///     Whether the handler qualifies for the plan's direct-construction factory
///     (<c>static () => new THandler()</c>); see
///     <see cref="RegistrableTypeModel.IsDirectlyConstructible"/>.
/// </param>
/// <param name="ProviderConstructionExpression">
///     The provider-taking construction factory for a dependency-injected handler, or
///     <c>null</c>; see <see cref="RegistrableTypeModel.ProviderConstructionExpression"/>.
/// </param>
/// <param name="UsesKeyedServices">
///     Whether the provider factory needs the keyed-service extensions; see
///     <see cref="RegistrableTypeModel.ProviderConstructionUsesKeyedServices"/>.
/// </param>
internal readonly record struct VoidPlanModel(
    string MessageTypeExpression,
    string HandlerTypeExpression,
    bool HasDirectConstruction,
    string? ProviderConstructionExpression,
    bool UsesKeyedServices);
