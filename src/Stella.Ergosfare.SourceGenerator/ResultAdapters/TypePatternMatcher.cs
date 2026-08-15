using System.Text;

namespace Stella.Ergosfare.SourceGenerator.ResultAdapters;

/// <summary>
///     Unifies an open adapter definition's carrier patterns with a concrete result slot.
///     A pattern is a type expression whose bare identifiers are the definition's type
///     parameters, so <c>IReadOnlyList&lt;T&gt;</c> matched against
///     <c>IReadOnlyList&lt;TodoDto&gt;</c> binds <c>T</c> to <c>TodoDto</c>.
/// </summary>
/// <param name="parameterNames">
///     The definition's type parameter names, positionally aligned with the bindings array
///     the match fills.
/// </param>
internal sealed class TypePatternMatcher(string[] parameterNames)
{
    /// <summary>
    ///     Structurally unifies a carrier pattern with a concrete slot expression, binding
    ///     parameters by position; a parameter met twice must bind identically. Arrays,
    ///     pointers and tuples are not unified through — their patterns only match
    ///     textually, mirroring the runtime unifier.
    /// </summary>
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

    /// <summary>Substitutes bound parameters back into a pattern, reproducing the display format.</summary>
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
