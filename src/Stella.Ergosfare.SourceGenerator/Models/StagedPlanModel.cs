using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

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
