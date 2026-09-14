using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// Comparison helpers the plugin models share.
/// </summary>
internal static class PluginModelExtensions
{
    /// <summary>
    /// Compares two arrays element by element, treating an uninitialized array as an empty
    /// one.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="left">The first array.</param>
    /// <param name="right">The second array.</param>
    /// <returns><c>true</c> when both hold the same elements in the same order.</returns>
    /// <remarks>
    /// Discovery produces both shapes, and the incremental pipeline caches on value
    /// equality — so an uninitialized array and an empty one have to compare equal.
    /// </remarks>
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
