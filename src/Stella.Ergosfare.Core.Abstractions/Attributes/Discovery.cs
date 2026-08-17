using System.Reflection;

namespace Stella.Ergosfare.Core.Abstractions.Attributes;

/// <summary>
/// Evaluates the discovery attributes at runtime — <see cref="ExcludeFromDiscoveryAttribute"/>,
/// <see cref="DiscoveryKeyAttribute"/>, and the key patterns registration calls pass.
/// </summary>
/// <remarks>
/// Generated registration applies the same rules at compile time; this type is the runtime
/// equivalent, for callers that decide at runtime whether a type would be discovered.
/// </remarks>
public static class Discovery
{
    /// <summary>
    /// Reports whether discovery with <paramref name="discoveryKeyPattern"/> selects
    /// <paramref name="type"/> — that is, the type is not excluded and at least one of its
    /// keys matches the pattern.
    /// </summary>
    /// <param name="type">The candidate type.</param>
    /// <param name="discoveryKeyPattern">
    /// An exact key, a prefix pattern ending in <c>*</c> such as <c>"reporting.*"</c>, or
    /// the empty string for key-less discovery.
    /// </param>
    /// <returns><c>true</c> when the type would be discovered.</returns>
    public static bool Matches(Type type, string discoveryKeyPattern)
    {
        if (IsExcluded(type))
        {
            return false;
        }

        foreach (var key in GetKeys(type))
        {
            if (MatchesKey(key, discoveryKeyPattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reports whether <paramref name="type"/> or its assembly carries
    /// <see cref="ExcludeFromDiscoveryAttribute"/>.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <returns><c>true</c> when the type is excluded from discovery.</returns>
    public static bool IsExcluded(Type type)
        => type.IsDefined(typeof(ExcludeFromDiscoveryAttribute), inherit: false)
           || type.Assembly.IsDefined(typeof(ExcludeFromDiscoveryAttribute));

    /// <summary>
    /// Returns the discovery keys in effect for <paramref name="type"/>: the keys it
    /// declares, falling back to its assembly's keys, falling back to
    /// <see cref="DiscoveryKeyAttribute.DefaultKey"/>.
    /// </summary>
    /// <param name="type">The type to read keys for.</param>
    /// <returns>The effective keys; never empty.</returns>
    public static string[] GetKeys(Type type)
    {
        var declared = type.GetCustomAttribute<DiscoveryKeyAttribute>(inherit: false)?.Keys;

        if (declared is not { Length: > 0 })
        {
            declared = type.Assembly.GetCustomAttribute<DiscoveryKeyAttribute>()?.Keys;
        }

        return declared is { Length: > 0 } ? declared : [DiscoveryKeyAttribute.DefaultKey];
    }

    /// <summary>
    /// Reports whether one discovery key matches a pattern: ordinal equality, or an ordinal
    /// prefix match against everything before the pattern's trailing <c>*</c>.
    /// </summary>
    /// <param name="key">The key to test.</param>
    /// <param name="discoveryKeyPattern">The pattern to test against.</param>
    /// <returns><c>true</c> when the key matches.</returns>
    public static bool MatchesKey(string key, string discoveryKeyPattern)
    {
        if (discoveryKeyPattern.Length > 0 && discoveryKeyPattern[^1] == '*')
        {
            return key.Length >= discoveryKeyPattern.Length - 1
                   && string.CompareOrdinal(key, 0, discoveryKeyPattern, 0, discoveryKeyPattern.Length - 1) == 0;
        }

        return string.Equals(key, discoveryKeyPattern, StringComparison.Ordinal);
    }
}
