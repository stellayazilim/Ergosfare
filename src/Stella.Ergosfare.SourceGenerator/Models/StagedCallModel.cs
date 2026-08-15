namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// One staged interceptor call: the concrete type to resolve, the arm to invoke it
/// through, and — when the participant qualifies — the bare <c>new T(...)</c> expression
/// the direct-construction variant substitutes for the container resolution.
/// </summary>
/// <param name="TypeExpression">The participant's <c>typeof</c>-qualified type expression.</param>
/// <param name="Arm">The pattern-match arm the runtime strategy would invoke it through.</param>
/// <param name="ConstructionExpression">The direct-construction expression, or <c>null</c>.</param>
/// <param name="ExceptionFilterExpression">
///     For an exception-stage call whose participant accepts only one exception type, that
///     type — emitted as an <c>is</c> guard around the call, standing in for the runtime
///     stage's filter probe. <c>null</c> means the call is unguarded.
/// </param>
/// <param name="GroupGuard">
///     For a call inside a group-filtering plan, the name of the local holding this
///     participant's group test — the body evaluates every guard once and then puts one
///     boolean in front of each call. <c>null</c> in a plan keyed by a proven set, where
///     participation is a compile-time fact and no call needs a guard.
/// </param>
internal readonly record struct StagedCallModel(
    string TypeExpression,
    StagedCallArm Arm,
    string? ConstructionExpression,
    string? ExceptionFilterExpression = null,
    string? GroupGuard = null);
