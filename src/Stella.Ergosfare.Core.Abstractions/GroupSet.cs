using System.Collections;
using System.Collections.Concurrent;

namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// An immutable group filter whose equal instances are canonicalized, so dispatch caches
/// can recognize a reused filter by reference instead of comparing names.
/// </summary>
/// <remarks>
/// <para>
/// Build a filter once with <see cref="Of"/> and reuse the instance:
/// <code>
/// static readonly GroupSet Reporting = GroupSet.Of("reporting");
/// await mediator.SendAsync(new BuildDailyReport(), Reporting);
/// </code>
/// A set is an <see cref="IReadOnlyList{T}"/> of its names, so it is accepted anywhere a
/// group sequence is, including the dispatch overloads that take contextual items.
/// </para>
/// <para>
/// Canonicalization is capped. Past the internal limit <see cref="Of"/> returns
/// non-canonical instances; those still dispatch identically, the caches simply compare
/// names. The cap only guards against group names generated dynamically without bound.
/// </para>
/// </remarks>
public sealed class GroupSet : IReadOnlyList<string>
{
    private const int InternCap = 1024;

    private static readonly ConcurrentDictionary<string, GroupSet> Interned = new();

    /// <summary>
    /// The filter that applies no group filtering, selecting the default pipeline.
    /// </summary>
    public static readonly GroupSet Empty = new([], string.Empty);

    private readonly string[] _names;

    /// <summary>
    /// The names joined with the separator the executor caches key grouped entries by,
    /// computed once per set.
    /// </summary>
    internal readonly string JoinedKey;

    private GroupSet(string[] names, string joinedKey)
    {
        _names = names;
        JoinedKey = joinedKey;
    }

    /// <summary>
    /// Returns the canonical set for <paramref name="groups"/>. Two calls with the same
    /// names in the same order return the same instance, up to the canonicalization cap.
    /// </summary>
    /// <param name="groups">
    /// The group names. Order is significant and names are compared ordinally, matching
    /// dispatch-time group semantics. The sequence is copied, so mutating the argument
    /// afterwards does not affect the returned set.
    /// </param>
    /// <returns>
    /// <see cref="Empty"/> when <paramref name="groups"/> is empty; otherwise a set over
    /// the given names.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="groups"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="groups"/> contains a <c>null</c> name.</exception>
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

        // Past the cap the set is handed out without being cached: callers lose reference
        // identity across Of calls, but the caches still match it by content.
        return Interned.Count < InternCap ? Interned.GetOrAdd(key, set) : set;
    }

    /// <summary>
    /// The backing name array, in order.
    /// </summary>
    internal string[] Names => _names;

    /// <summary>
    /// The number of group names in this set.
    /// </summary>
    public int Count => _names.Length;

    /// <summary>
    /// The group name at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based position to read.</param>
    public string this[int index] => _names[index];

    /// <summary>
    /// Returns an enumerator over the group names, in order.
    /// </summary>
    public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)_names).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Returns the group names for display, or <c>GroupSet.Empty</c> when there are none.
    /// </summary>
    public override string ToString()
        => _names.Length == 0 ? "GroupSet.Empty" : $"GroupSet [{string.Join(", ", _names)}]";
}
