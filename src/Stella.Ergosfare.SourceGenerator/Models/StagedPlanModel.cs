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
    string? HandlerConstructionExpression,
    ImmutableArray<StagedCallModel> PreCalls,
    ImmutableArray<StagedCallModel> PostCalls,
    ImmutableArray<StagedCallModel> ExceptionCalls,
    ImmutableArray<StagedCallModel> FinalCalls)
{
    /// <summary>
    ///     Whether every participant — the handler and each interceptor — carries a
    ///     construction expression, making the plan eligible for the emitted
    ///     direct-construction variant (<c>ExecuteDirect</c>).
    /// </summary>
    public bool SupportsDirectConstruction
        => HandlerConstructionExpression is not null
           && AllConstructible(PreCalls)
           && AllConstructible(PostCalls)
           && AllConstructible(ExceptionCalls)
           && AllConstructible(FinalCalls);

    private static bool AllConstructible(ImmutableArray<StagedCallModel> calls)
    {
        foreach (var call in calls)
        {
            if (call.ConstructionExpression is null)
            {
                return false;
            }
        }

        return true;
    }
}
