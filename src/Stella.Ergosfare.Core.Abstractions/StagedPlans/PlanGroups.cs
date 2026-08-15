using Stella.Ergosfare.Core.Abstractions.Attributes;

namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>
/// The group test a group-filtering plan bakes around each participant — the runtime
/// shape-builder's <c>MatchesAnyGroup</c>, called from generated code instead of being
/// walked over a materialized composition.
/// </summary>
/// <remarks>
/// A plan keyed by a proven group set needs none of this: its participants are decided at
/// compile time. This serves the other case — a dispatch whose filter is a runtime value —
/// where the set cannot key a plan but the pipeline can still be straight-line code with a
/// boolean in front of each call.
/// </remarks>
public static class PlanGroups
{
    /// <summary>
    /// Whether a participant declaring no <c>[Group]</c> runs under the requested set: it
    /// belongs to the default group, and an empty request IS the default group.
    /// </summary>
    public static bool MatchesDefault(IReadOnlyList<string> requested)
    {
        if (requested.Count == 0)
        {
            return true;
        }

        for (var i = 0; i < requested.Count; i++)
        {
            if (string.Equals(requested[i], GroupAttribute.DefaultGroupName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a participant declaring exactly one group runs under the requested set — the
    /// overwhelmingly common shape, spelled without an array so the emitted guard is a
    /// string compare over the request.
    /// </summary>
    public static bool Matches(IReadOnlyList<string> requested, string declared)
    {
        if (requested.Count == 0)
        {
            // The default group was asked for; a participant that named a group is not in it.
            return string.Equals(declared, GroupAttribute.DefaultGroupName, StringComparison.Ordinal);
        }

        for (var i = 0; i < requested.Count; i++)
        {
            if (string.Equals(requested[i], declared, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Any-of × any-of, for a participant declaring several groups.</summary>
    public static bool Matches(IReadOnlyList<string> requested, string[] declared)
    {
        for (var i = 0; i < declared.Length; i++)
        {
            if (Matches(requested, declared[i]))
            {
                return true;
            }
        }

        return false;
    }
}
