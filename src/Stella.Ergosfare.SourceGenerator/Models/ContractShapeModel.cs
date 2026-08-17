namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// One interceptor contract exactly as a type implements it, before contracts are reduced
/// to registrations.
/// </summary>
/// <param name="Kind">
/// The stage the contract belongs to; never <see cref="DescriptorKind.MainHandler"/>.
/// </param>
/// <param name="IsAsync">Whether this is the asynchronous form of the contract.</param>
/// <param name="IsResultTyped">Whether the contract names the result type.</param>
/// <param name="MessageTypeExpression">
/// The message type the contract is registered against, normalized the same way a
/// registration's is.
/// </param>
/// <param name="ResultTypeExpression">
/// The declared result type when the contract names one; otherwise <c>null</c>.
/// </param>
/// <param name="ExceptionFilterExpression">
/// The failure type the interceptor accepts, read from its filter contract, or <c>null</c>
/// when it accepts every failure. Only ever set on an exception-stage shape.
/// </param>
/// <param name="HasUndecidableExceptionFilter">
/// Whether the interceptor filters in a way that cannot be reproduced at compile time — a
/// hand-written filter, or several typed ones, so the accepted type is not a single
/// constant. Such an interceptor disqualifies the plan, and the dispatch falls back to the
/// general stage, which asks the instance itself.
/// </param>
/// <remarks>
/// This is the undeduplicated counterpart of <see cref="DescriptorModel"/>: it keeps the
/// asynchronous/synchronous and typed/untyped facts that staged emission needs in order to
/// choose the same contract the runtime strategy would.
/// </remarks>
internal readonly record struct ContractShapeModel(
    DescriptorKind Kind,
    bool IsAsync,
    bool IsResultTyped,
    string MessageTypeExpression,
    string? ResultTypeExpression,
    string? ExceptionFilterExpression = null,
    bool HasUndecidableExceptionFilter = false);
