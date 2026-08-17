namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// One participant's registration, worked out at compile time instead of reflectively at
/// startup.
/// </summary>
/// <param name="Kind">Which stage the participant belongs to.</param>
/// <param name="MessageTypeExpression">
/// The message type to write inside <c>typeof</c>: as declared for a main handler, and
/// normalized to the generic definition for an interceptor.
/// </param>
/// <param name="ResultTypeExpression">
/// The result type to write inside <c>typeof</c>: the declared result for a synchronous
/// contract, a task carrier for an asynchronous one, <c>object</c> for a contract that does
/// not name the result, and <c>null</c> for a pre-interceptor, which has no result at all.
/// </param>
internal readonly record struct DescriptorModel(
    DescriptorKind Kind,
    string MessageTypeExpression,
    string? ResultTypeExpression);
