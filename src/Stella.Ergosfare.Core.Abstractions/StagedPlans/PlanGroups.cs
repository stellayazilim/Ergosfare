using Stella.Ergosfare.Core.Abstractions.Attributes;

namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>
/// The group tests generated plans call before each participant.
/// </summary>
/// <remarks>
/// A plan compiled for a known group set needs none of these — which participants run was
/// decided at compile time. These serve the other case, a dispatch whose groups are only
/// known at runtime, where the pipeline can still be straight-line code with a test in
/// front of each call.
/// </remarks>
public static class PlanGroups
{
    /// <summary>
    /// Reports whether a participant that declares no groups runs under
    /// <paramref name="requested"/>.
    /// </summary>
    /// <param name="requested">The groups the dispatch asked for.</param>
    /// <returns>
    /// <c>true</c> when the default group was asked for, either by naming it or by asking
    /// for nothing.
    /// </returns>
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
    /// Reports whether a participant that declares one group runs under
    /// <paramref name="requested"/>.
    /// </summary>
    /// <param name="requested">The groups the dispatch asked for.</param>
    /// <param name="declared">The group the participant declared.</param>
    /// <returns><c>true</c> when the declared group was asked for.</returns>
    /// <remarks>
    /// Declaring a single group is the common shape, so it is spelled without an array and
    /// the generated guard is a string comparison over the request.
    /// </remarks>
    public static bool Matches(IReadOnlyList<string> requested, string declared)
    {
        if (requested.Count == 0)
        {
            // Asking for nothing asks for the default group, which a participant that named
            // a group is only in if it named that one.
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

    /// <summary>
    /// Reports whether a participant that declares several groups runs under
    /// <paramref name="requested"/>.
    /// </summary>
    /// <param name="requested">The groups the dispatch asked for.</param>
    /// <param name="declared">The groups the participant declared.</param>
    /// <returns><c>true</c> when any declared group was asked for.</returns>
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
