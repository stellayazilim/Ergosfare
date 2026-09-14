namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// One interceptor call inside a staged plan.
/// </summary>
/// <param name="TypeExpression">The participant's fully qualified type.</param>
/// <param name="Arm">Which contract to call it through.</param>
/// <param name="ConstructionExpression">
/// How to construct the participant without the container, when it qualifies for that;
/// otherwise <c>null</c>.
/// </param>
/// <param name="ExceptionFilterExpression">
/// The exception type an exception-stage participant accepts, written as a type test around
/// the call in place of the runtime stage's filter. <c>null</c> leaves the call unguarded.
/// </param>
/// <param name="GroupGuard">
/// The local holding this participant's group test, in a filtering plan: the body computes
/// every test once and then puts one boolean in front of each call. <c>null</c> in a plan
/// compiled for a known group set, where participation is already decided.
/// </param>
internal readonly record struct StagedCallModel(
    string TypeExpression,
    StagedCallArm Arm,
    string? ConstructionExpression,
    string? ExceptionFilterExpression = null,
    string? GroupGuard = null);
