using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     The pattern-match arm the runtime invocation strategy would select for one staged
///     interceptor call — determined at compile time from the interceptor's implemented
///     contracts, so the emitted call invokes exactly the member the strategy would.
/// </summary>
internal enum StagedCallArm : byte
{
    /// <summary>The result-typed asynchronous contract (first arm of the runtime pattern match).</summary>
    AsyncTyped,

    /// <summary>The result-agnostic asynchronous contract (second arm; also the sole async pre arm).</summary>
    AsyncAgnostic,

    /// <summary>The synchronous contract (last arm).</summary>
    Sync,
}

/// <summary>One staged interceptor call: the concrete type to resolve and the arm to invoke it through.</summary>
internal readonly record struct StagedCallModel(string TypeExpression, StagedCallArm Arm);

/// <summary>
///     A staged pipeline plan ready for emission: a message whose whole discovered
///     pipeline — one async handler plus at least one interceptor stage — could be
///     modeled exactly, with every stage in the runtime shape-builder's execution order
///     (direct first, then indirect, each segment weight-descending then ordinal by type
///     name) and every call's pattern-match arm resolved at compile time. Advisory like
///     every plan: the hosting executor re-validates the baked composition per registry
///     version and falls back to the runtime strategy on any mismatch.
/// </summary>
internal sealed record StagedPlanModel(
    string MessageTypeExpression,
    string? ResultTypeExpression,
    bool ResultIsValueType,
    string HandlerTypeExpression,
    ImmutableArray<StagedCallModel> PreCalls,
    ImmutableArray<StagedCallModel> PostCalls,
    ImmutableArray<StagedCallModel> ExceptionCalls,
    ImmutableArray<StagedCallModel> FinalCalls);
