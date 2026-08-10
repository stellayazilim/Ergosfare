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
/// <param name="ExceptionFilterExpression">
///     The exception type the declaring interceptor accepts, read off its
///     <c>IExceptionInterceptorFilter&lt;TException&gt;</c>; <c>null</c> when it accepts
///     every exception. Only ever set on <see cref="DescriptorKind.ExceptionInterceptor"/>
///     shapes.
/// </param>
/// <param name="HasUndecidableExceptionFilter">
///     Whether the declaring interceptor filters in a way the generator cannot reproduce —
///     a hand-written non-generic filter, or several generic ones, where the exception type
///     is not a single compile-time constant. Such a plan is disqualified so the dispatch
///     falls back to the runtime stage, which asks the instance itself.
/// </param>
internal readonly record struct ContractShapeModel(
    DescriptorKind Kind,
    bool IsAsync,
    bool IsResultTyped,
    string MessageTypeExpression,
    string? ResultTypeExpression,
    string? ExceptionFilterExpression = null,
    bool HasUndecidableExceptionFilter = false);
