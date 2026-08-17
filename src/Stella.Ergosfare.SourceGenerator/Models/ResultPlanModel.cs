namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// A plan for a message with a single result contract whose whole pipeline is one
/// asynchronous handler, discovered by default and in the default group.
/// </summary>
/// <param name="MessageTypeExpression">The closed message type.</param>
/// <param name="ResultTypeExpression">The closed result type.</param>
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
/// The result counterpart of <see cref="VoidPlanModel"/>, checked against the container's
/// composition at runtime in exactly the same way.
/// </remarks>
internal readonly record struct ResultPlanModel(
    string MessageTypeExpression,
    string ResultTypeExpression,
    string HandlerTypeExpression,
    bool HasDirectConstruction,
    string? ProviderConstructionExpression,
    bool UsesKeyedServices);
