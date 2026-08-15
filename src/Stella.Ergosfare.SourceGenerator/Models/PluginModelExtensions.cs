using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

internal static class PluginModelExtensions
{
    /// <summary>
    ///     Sequence equality that treats a default array as empty — discovery leaves both
    ///     shapes behind and the incremental pipeline caches on value equality.
    /// </summary>
    internal static bool SequenceEqualOrBothEmpty<T>(this ImmutableArray<T> left, ImmutableArray<T> right)
    {
        if (left.IsDefaultOrEmpty)
        {
            return right.IsDefaultOrEmpty;
        }

        if (right.IsDefaultOrEmpty || left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }
}
