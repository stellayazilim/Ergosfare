namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// A plan for a message that produces no result and whose whole pipeline is a single
/// asynchronous handler, discovered by default and in the default group.
/// </summary>
/// <param name="MessageTypeExpression">The closed message type.</param>
/// <param name="HandlerTypeExpression">The one handler that serves it.</param>
/// <param name="HasDirectConstruction">
/// Whether the handler can be constructed without the container; see
/// <see cref="RegistrableTypeModel.IsDirectlyConstructible"/>.
/// </param>
/// <param name="ProviderConstructionExpression">
/// How to construct a handler that takes constructor dependencies, resolving them from the
/// dispatching provider, or <c>null</c>; see
/// <see cref="RegistrableTypeModel.ProviderConstructionExpression"/>.
/// </param>
/// <param name="UsesKeyedServices">
/// Whether that construction needs the keyed-service extensions; see
/// <see cref="RegistrableTypeModel.ProviderConstructionUsesKeyedServices"/>.
/// </param>
/// <remarks>
/// Written out so the executor closes over both types and calls the handler directly. The
/// runtime still checks the plan against the composition the container selected, so a plan
/// that no longer matches loses its speedup and nothing else.
/// </remarks>
internal readonly record struct VoidPlanModel(
    string MessageTypeExpression,
    string HandlerTypeExpression,
    bool HasDirectConstruction,
    string? ProviderConstructionExpression,
    bool UsesKeyedServices);
