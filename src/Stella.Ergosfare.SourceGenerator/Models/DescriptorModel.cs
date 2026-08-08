namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// Value-equatable projection of one pre-computed handler descriptor: everything the
/// runtime descriptor builders would have derived reflectively, resolved at compile time.
/// </summary>
/// <param name="Kind">Which descriptor kind (and therefore which factory method) to emit.</param>
/// <param name="MessageTypeExpression">
/// The <c>typeof</c> argument for the descriptor's message type — verbatim for main
/// handlers, normalized to the generic definition for interceptors, mirroring the runtime
/// builders.
/// </param>
/// <param name="ResultTypeExpression">
/// The <c>typeof</c> argument for the descriptor's result type: the declared result for
/// synchronous contracts, a <c>ValueTask</c> carrier for asynchronous ones, <c>object</c>
/// for result-agnostic interceptor contracts, and <c>null</c> for pre-interceptors (which
/// carry no result type).
/// </param>
internal readonly record struct DescriptorModel(
    DescriptorKind Kind,
    string MessageTypeExpression,
    string? ResultTypeExpression);
