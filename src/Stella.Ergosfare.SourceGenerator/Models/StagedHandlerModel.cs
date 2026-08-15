using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     One main handler of a staged plan, with the construction expression the
///     direct-construction variant uses when the participant qualifies.
/// </summary>
/// <remarks>
///     No pattern-match arm travels with a handler the way it does with an interceptor
///     (<see cref="StagedCallModel"/>): a plan is only computed when every handler it calls
///     is the asynchronous contract, so the emitted call is always the same member on the
///     concrete type. Anything else disqualifies the plan and the runtime strategy serves the
///     message. A single-handler plan's covariant segment is not called, so its contracts are
///     never asked about — only the types, which the gate compares.
/// </remarks>
internal readonly record struct StagedHandlerModel(
    string TypeExpression,
    string? ConstructionExpression,
    string? GroupGuard = null);
