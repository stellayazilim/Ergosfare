namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     A compile-time result pipeline plan: a dispatchable command/query message with a
///     single closed result contract whose entire discovered pipeline is one
///     default-discovery, default-group async result handler. Emitted as
///     <c>GeneratedDispatchRoots.AddResultPlan&lt;TMessage, TResult, THandler&gt;()</c> so
///     the runtime executor closes over all three types and calls the handler
///     devirtualized. Advisory exactly like the void plan: the runtime re-validates the
///     pipeline per registry version and a mismatched plan only loses the speedup.
/// </summary>
/// <param name="MessageTypeExpression">Fully qualified expression of the closed message type.</param>
/// <param name="ResultTypeExpression">Fully qualified expression of the closed result type.</param>
/// <param name="HandlerTypeExpression">Fully qualified expression of the sole handler type.</param>
/// <param name="HasDirectConstruction">
///     Whether the handler qualifies for the plan's direct-construction factory; see
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
internal readonly record struct ResultPlanModel(
    string MessageTypeExpression,
    string ResultTypeExpression,
    string HandlerTypeExpression,
    bool HasDirectConstruction,
    string? ProviderConstructionExpression,
    bool UsesKeyedServices);
