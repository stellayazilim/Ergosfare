namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     One raw interceptor contract a type implements — the undeduped counterpart of
///     <see cref="DescriptorModel"/>, keeping the async/sync and result-typed/agnostic
///     facts the staged-plan emission needs to reproduce the invocation strategies'
///     pattern-match arm selection at compile time.
/// </summary>
/// <param name="Kind">The interceptor stage the contract belongs to; never <see cref="DescriptorKind.MainHandler"/>.</param>
/// <param name="IsAsync">Whether the contract is the asynchronous flavor.</param>
/// <param name="IsResultTyped">Whether the contract carries a typed result parameter (arity-2 for post/exception/final).</param>
/// <param name="MessageTypeExpression">The contract's registered message type, normalized like descriptor message types.</param>
/// <param name="ResultTypeExpression">The declared result type for result-typed contracts, <c>null</c> otherwise.</param>
internal readonly record struct ContractShapeModel(
    DescriptorKind Kind,
    bool IsAsync,
    bool IsResultTyped,
    string MessageTypeExpression,
    string? ResultTypeExpression);
