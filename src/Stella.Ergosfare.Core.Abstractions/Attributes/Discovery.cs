using System;
using System.Reflection;

namespace Stella.Ergosfare.Core.Abstractions.Attributes;

/// <summary>
/// Runtime evaluation of the discovery attributes — the single source of truth for how
/// <see cref="ExcludeFromDiscoveryAttribute"/>, <see cref="DiscoveryKeyAttribute"/> and key
/// patterns compose. The reflection-based scanning paths (<c>RegisterFromAssembly</c>) call
/// into this so their semantics stay identical to source-generated registration, which
/// evaluates the same rules at compile time.
/// </summary>
public static class Discovery
{
    /// <summary>
    /// Whether discovery with the given key pattern selects the type: the type is not
    /// excluded from discovery, and at least one of its effective discovery keys matches
    /// the pattern.
    /// </summary>
    /// <param name="type">The candidate registrable type.</param>
    /// <param name="discoveryKeyPattern">
    /// The key pattern: an exact key, a prefix glob with a trailing <c>*</c>
    /// (e.g. <c>"reporting.*"</c>), or the empty string for default discovery.
    /// </param>
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
    /// Whether the type — or its assembly — opts out of discovery via
    /// <see cref="ExcludeFromDiscoveryAttribute"/>.
    /// </summary>
    public static bool IsExcluded(Type type)
        => type.IsDefined(typeof(ExcludeFromDiscoveryAttribute), inherit: false)
           || type.Assembly.IsDefined(typeof(ExcludeFromDiscoveryAttribute));

    /// <summary>
    /// The type's effective discovery keys: its own <see cref="DiscoveryKeyAttribute"/>
    /// keys when declared, else its assembly's, else the implicit default key.
    /// </summary>
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
    /// Whether a single discovery key matches a pattern: ordinal equality, or — when the
    /// pattern ends with <c>*</c> — an ordinal prefix match on the part before the star.
    /// </summary>
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
