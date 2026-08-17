namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// One main handler of a staged plan.
/// </summary>
/// <param name="TypeExpression">The handler's fully qualified type.</param>
/// <param name="ConstructionExpression">
/// How to construct the handler without the container, when it qualifies for that; otherwise
/// <c>null</c>.
/// </param>
/// <param name="GroupGuard">
/// The group test guarding the call in a filtering plan, or <c>null</c> when the call is
/// unconditional.
/// </param>
/// <remarks>
/// No contract choice travels with a handler the way it does with an interceptor
/// (<see cref="StagedCallModel"/>). A plan is only produced when every handler it calls
/// implements the asynchronous contract, so the call is always the same member on the
/// concrete type; anything else means no plan and the general strategy serves the message. A
/// single-handler plan never calls its covariant segment, so those contracts are never
/// examined — only their types, which the plan's check compares.
/// </remarks>
internal readonly record struct StagedHandlerModel(
    string TypeExpression,
    string? ConstructionExpression,
    string? GroupGuard = null);
