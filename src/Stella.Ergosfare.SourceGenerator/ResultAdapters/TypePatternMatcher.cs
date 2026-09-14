using System.Text;

namespace Stella.Ergosfare.SourceGenerator.ResultAdapters;

/// <summary>
/// Matches an open adapter definition's declared patterns against a concrete result type.
/// </summary>
/// <param name="parameterNames">
/// The definition's type parameter names, in the same order as the bindings a match fills.
/// </param>
/// <remarks>
/// A pattern is a type expression whose bare identifiers are the definition's type
/// parameters, so <c>IReadOnlyList&lt;T&gt;</c> matched against
/// <c>IReadOnlyList&lt;TodoDto&gt;</c> binds <c>T</c> to <c>TodoDto</c>.
/// </remarks>
internal sealed class TypePatternMatcher(string[] parameterNames)
{
    /// <summary>
    /// Matches a pattern against a concrete type, binding type parameters as it goes.
    /// </summary>
    /// <param name="pattern">The declared pattern.</param>
    /// <param name="concrete">The concrete type to match against.</param>
    /// <param name="bindings">
    /// The bindings so far, indexed by parameter position and filled in as the match
    /// proceeds.
    /// </param>
    /// <returns>
    /// <c>true</c> when the two match. A parameter appearing more than once must bind to the
    /// same type each time.
    /// </returns>
    /// <remarks>
    /// Arrays, pointers and tuples are not taken apart — their patterns match textually,
    /// which is what the runtime unifier does too.
    /// </remarks>
    internal bool TryMatch(string pattern, string concrete, string?[] bindings)
    {
        var parameterPosition = Array.IndexOf(parameterNames, pattern);

        if (parameterPosition >= 0)
        {
            if (bindings[parameterPosition] is { } bound)
            {
                return bound == concrete;
            }

            bindings[parameterPosition] = concrete;
            return true;
        }

        // Not a parameter and not generic: the two have to be the same text.
        if (!TypeExpressions.TrySplitGeneric(pattern, out var patternName, out var patternArguments))
        {
            return pattern == concrete;
        }

        if (!TypeExpressions.TrySplitGeneric(concrete, out var concreteName, out var concreteArguments)
            || patternName != concreteName
            || patternArguments.Count != concreteArguments.Count)
        {
            return false;
        }

        for (var i = 0; i < patternArguments.Count; i++)
        {
            if (!TryMatch(patternArguments[i], concreteArguments[i], bindings))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Writes a pattern back out with its parameters replaced by what they bound to.
    /// </summary>
    /// <param name="pattern">The pattern to write.</param>
    /// <param name="bindings">The bindings a match produced.</param>
    /// <returns>
    /// The pattern with every bound parameter substituted, in the same format a type
    /// expression is written in.
    /// </returns>
    internal string Render(string pattern, string?[] bindings)
    {
        var parameterPosition = Array.IndexOf(parameterNames, pattern);

        if (parameterPosition >= 0)
        {
            return bindings[parameterPosition] ?? pattern;
        }

        if (!TypeExpressions.TrySplitGeneric(pattern, out var name, out var arguments))
        {
            return pattern;
        }

        var rendered = new StringBuilder(name).Append('<');

        for (var i = 0; i < arguments.Count; i++)
        {
            rendered.Append(i == 0 ? string.Empty : ", ").Append(Render(arguments[i], bindings));
        }

        return rendered.Append('>').ToString();
    }
}
