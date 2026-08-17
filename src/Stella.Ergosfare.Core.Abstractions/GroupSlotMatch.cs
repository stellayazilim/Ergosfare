namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// Compares a caller's group sequence against the names a cached dispatch slot was built
/// from. Shared by every dispatch surface that keeps a last-used (groups → pipeline) slot.
/// </summary>
internal static class GroupSlotMatch
{
    /// <summary>
    /// Reports whether <paramref name="groups"/> selects the same pipeline as the slot
    /// built from <paramref name="cachedNames"/>.
    /// </summary>
    /// <param name="groups">The caller's group sequence.</param>
    /// <param name="cachedNames">The names the slot was built from.</param>
    /// <param name="canonical">
    /// The canonical set the slot was built from, when it was built from one.
    /// </param>
    /// <returns><c>true</c> when the slot serves this dispatch.</returns>
    /// <remarks>
    /// A <see cref="GroupSet"/> that is the very instance the slot was built from matches
    /// on a reference check; canonicalization makes that the steady state for callers who
    /// reuse a filter. Anything else falls through to the element-wise compare. The
    /// shortcut is limited to <see cref="GroupSet"/> because it is immutable — a caller's
    /// reused list must keep being compared by content so in-place mutation is observed.
    /// </remarks>
    internal static bool Matches(IEnumerable<string> groups, string[] cachedNames, GroupSet? canonical)
        => groups is GroupSet set
            ? ReferenceEquals(set, canonical) || SequenceMatches(set, cachedNames)
            : SequenceMatches(groups, cachedNames);

    /// <summary>
    /// Compares <paramref name="groups"/> against <paramref name="cached"/> element-wise
    /// and ordinally. Order is significant, matching how the joined cache key is built.
    /// </summary>
    /// <param name="groups">The caller's group sequence.</param>
    /// <param name="cached">The names to compare against.</param>
    /// <returns><c>true</c> when both sequences hold the same names in the same order.</returns>
    /// <remarks>
    /// The <see cref="GroupSet"/>, array and <see cref="List{T}"/> shapes are indexed
    /// directly; every other sequence is enumerated once.
    /// </remarks>
    internal static bool SequenceMatches(IEnumerable<string> groups, string[] cached)
    {
        switch (groups)
        {
            case GroupSet set:
            {
                var names = set.Names;

                if (names.Length != cached.Length)
                {
                    return false;
                }

                for (var i = 0; i < names.Length; i++)
                {
                    if (!string.Equals(names[i], cached[i], StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                return true;
            }
            case string[] array:
            {
                if (array.Length != cached.Length)
                {
                    return false;
                }

                for (var i = 0; i < array.Length; i++)
                {
                    if (!string.Equals(array[i], cached[i], StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                return true;
            }
            case List<string> list:
            {
                if (list.Count != cached.Length)
                {
                    return false;
                }

                for (var i = 0; i < cached.Length; i++)
                {
                    if (!string.Equals(list[i], cached[i], StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                return true;
            }
            default:
            {
                // A sequence of unknown shape is walked once; the trailing check catches a
                // caller sequence that is a prefix of the cached names.
                var index = 0;

                foreach (var group in groups)
                {
                    if (index >= cached.Length || !string.Equals(group, cached[index], StringComparison.Ordinal))
                    {
                        return false;
                    }

                    index++;
                }

                return index == cached.Length;
            }
        }
    }
}
