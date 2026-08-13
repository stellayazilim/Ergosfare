namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// Compares a caller's group sequence against a cached snapshot of one. Every dispatch
/// surface that keeps a last-used (groups → pipeline) slot asks the same question, so the
/// answer lives once, next to the group vocabulary itself.
/// </summary>
/// <remarks>
/// This used to live inside the command/query executor cache, which left the broadcast
/// invoker reaching into that cache's internals to answer a question about its own slot.
/// The shared lookup this is being folded into needs one implementation anyway.
/// </remarks>
internal static class GroupSlotMatch
{
    /// <summary>
    /// Slot match with the canonical fast path: a <see cref="GroupSet"/> that is the very
    /// instance the slot was built from matches on one reference check — interning makes
    /// that the steady state for callers reusing a filter. Everything else (a different
    /// or un-interned set, a plain sequence) falls to the ordinal element-wise compare.
    /// Only immutable <see cref="GroupSet"/> instances take the reference shortcut; a
    /// reused mutable list must keep being compared by content so in-place mutation is
    /// always observed.
    /// </summary>
    internal static bool Matches(IEnumerable<string> groups, string[] cachedNames, GroupSet? canonical)
        => groups is GroupSet set
            ? ReferenceEquals(set, canonical) || SequenceMatches(set, cachedNames)
            : SequenceMatches(groups, cachedNames);

    /// <summary>
    /// Ordinal element-wise comparison of the caller's group sequence against a cached
    /// snapshot, allocation-free for the <see cref="GroupSet"/>, array and list shapes.
    /// Order is significant, matching the joined composite key exactly.
    /// </summary>
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
