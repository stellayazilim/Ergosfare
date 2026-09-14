using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
/// The canonical form of a group set, shared by everything that keys on one.
/// </summary>
internal static class GroupNames
{
    /// <summary>
    /// The group a participant declaring none belongs to, and the group an empty request
    /// means.
    /// </summary>
    internal const string Default = "default";

    /// <summary>
    /// Puts a group set into its canonical form: sorted ordinally, with duplicates removed.
    /// </summary>
    /// <param name="names">The group names, which this method sorts in place.</param>
    /// <returns>The canonical set, empty when <paramref name="names"/> is.</returns>
    /// <remarks>
    /// Group selection is an any-of test, so order and repetition select the same
    /// participants — which means two spellings of one set have to key one plan.
    /// </remarks>
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
            // Sorting put equal names next to each other, so comparing with the last one
            // kept is enough to drop duplicates.
            if (builder.Count == 0 || !string.Equals(builder[builder.Count - 1], name, StringComparison.Ordinal))
            {
                builder.Add(name);
            }
        }

        return builder.ToImmutable();
    }
}
