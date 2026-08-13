using System.Collections;
using System.Collections.Concurrent;

namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// An immutable, canonicalized group filter: <see cref="Of"/> interns equal sequences
/// (same names, same order — ordinal) to one instance, so the grouped dispatch caches can
/// match a reused filter with a single reference check instead of comparing group names
/// element-wise. Define filters once and reuse them:
/// <code>
/// static readonly GroupSet Reporting = GroupSet.Of("reporting");
/// await mediator.SendAsync(new BuildDailyReport(), Reporting);
/// </code>
/// A <see cref="GroupSet"/> is also an <see cref="IReadOnlyList{T}"/> of its names, so it can
/// be passed anywhere a group sequence is accepted — including the full dispatch overloads
/// that take one alongside contextual items — and the caches recognize it there too.
/// </summary>
/// <remarks>
/// Interning is bounded: beyond an internal cap, <see cref="Of"/> returns un-interned
/// instances, which still dispatch correctly — the caches fall back to comparing group
/// names. Group names come from code in practice, so the cap exists only as a guard
/// against pathological dynamic name generation.
/// </remarks>
public sealed class GroupSet : IReadOnlyList<string>
{
    private const int InternCap = 1024;

    private static readonly ConcurrentDictionary<string, GroupSet> Interned = new();

    /// <summary>The empty filter: no group filtering, the default pipeline.</summary>
    public static readonly GroupSet Empty = new([], string.Empty);

    private readonly string[] _names;

    /// <summary>
    /// The names joined with the executor caches' separator — the same string the
    /// composite stores key grouped executors by, precomputed once per set.
    /// </summary>
    internal readonly string JoinedKey;

    private GroupSet(string[] names, string joinedKey)
    {
        _names = names;
        JoinedKey = joinedKey;
    }

    /// <summary>
    /// Returns the canonical <see cref="GroupSet"/> for the given group names. Order is
    /// significant and comparison is ordinal, matching dispatch-time group semantics
    /// exactly; the input sequence is snapshotted, so later mutation of a passed array
    /// never affects the set.
    /// </summary>
    /// <param name="groups">The group names; must not be null or contain nulls.</param>
    public static GroupSet Of(params string[] groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        if (groups.Length == 0)
        {
            return Empty;
        }

        foreach (var group in groups)
        {
            if (group is null)
            {
                throw new ArgumentException("Group names must be non-null.", nameof(groups));
            }
        }

        var key = string.Join('\x1f', groups);

        if (Interned.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var set = new GroupSet([.. groups], key);

        // Beyond the cap the set is served un-interned: reference identity across Of
        // calls is lost, content-based matching in the caches is not.
        return Interned.Count < InternCap ? Interned.GetOrAdd(key, set) : set;
    }

    /// <summary>The group names, in order.</summary>
    internal string[] Names => _names;

    /// <inheritdoc />
    public int Count => _names.Length;

    /// <inheritdoc />
    public string this[int index] => _names[index];

    /// <inheritdoc />
    public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)_names).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public override string ToString()
        => _names.Length == 0 ? "GroupSet.Empty" : $"GroupSet [{string.Join(", ", _names)}]";
}
