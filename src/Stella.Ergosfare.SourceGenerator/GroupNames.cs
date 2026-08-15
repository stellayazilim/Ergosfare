using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
///     The canonical form of a group set, shared by everything that keys anything on one.
/// </summary>
internal static class GroupNames
{
    /// <summary>
    ///     The group a participant declaring no <c>[Group]</c> belongs to, and the group an
    ///     empty target set means.
    /// </summary>
    internal const string Default = "default";

    /// <summary>
    ///     Ordinal-sorted and deduplicated, because group selection is an any-of test —
    ///     order and repetition select the same participants, so two spellings of one set
    ///     must key one plan.
    /// </summary>
    internal static ImmutableArray<string> Normalize(List<string> names)
    {
        if (names.Count == 0)
        {
            return ImmutableArray<string>.Empty;
        }

        names.Sort(StringComparer.Ordinal);

        var builder = ImmutableArray.CreateBuilder<string>(names.Count);

        foreach (var name in names)
        {
            if (builder.Count == 0 || !string.Equals(builder[builder.Count - 1], name, StringComparison.Ordinal))
            {
                builder.Add(name);
            }
        }

        return builder.ToImmutable();
    }
}
