using System.Text;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
/// String operations on the type expressions the models carry — the fully qualified
/// spellings written into generated code.
/// </summary>
/// <remarks>
/// Nothing here reads a symbol; a type expression is the only input. That is what lets the
/// planning layer compare types it never resolved.
/// </remarks>
internal static class TypeExpressions
{
    private const string GlobalPrefix = "global::";

    /// <summary>
    /// Removes the global qualifier from a type expression.
    /// </summary>
    /// <param name="typeExpression">The expression to strip.</param>
    /// <returns>The expression without its qualifier, or unchanged if it had none.</returns>
    internal static string StripGlobalPrefix(string typeExpression)
        => typeExpression.StartsWith(GlobalPrefix, StringComparison.Ordinal)
            ? typeExpression.Substring(GlobalPrefix.Length)
            : typeExpression;

    /// <summary>
    /// Splits a generic type expression into its name and its top-level type arguments.
    /// </summary>
    /// <param name="expression">The expression to split.</param>
    /// <param name="name">The name before the argument list, when this returns <c>true</c>.</param>
    /// <param name="arguments">The top-level arguments, when this returns <c>true</c>.</param>
    /// <returns>
    /// <c>true</c> for a plain <c>Name&lt;…&gt;</c> shape. <c>false</c> for anything else,
    /// including shapes this does not take apart — tuples, arrays, and a nested type after a
    /// generic — which are compared as text instead.
    /// </returns>
    internal static bool TrySplitGeneric(string expression, out string name, out List<string> arguments)
    {
        name = expression;
        arguments = [];

        var open = expression.IndexOf('<');

        if (open < 0 || expression.Length == 0 || expression[expression.Length - 1] != '>')
        {
            return false;
        }

        name = expression.Substring(0, open);

        var depth = 0;
        var argumentStart = open + 1;

        for (var i = open; i < expression.Length; i++)
        {
            switch (expression[i])
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;

                    if (depth == 0 && i != expression.Length - 1)
                    {
                        // The outer list closed before the end, so this is not a plain
                        // Name<...> — "X<T>.Nested", for instance. Compare it as text.
                        return false;
                    }

                    break;
                case ',' when depth == 1:
                    // Only commas at the top level separate arguments; deeper ones belong to
                    // an argument of its own.
                    arguments.Add(expression.Substring(argumentStart, i - argumentStart).Trim());
                    argumentStart = i + 1;
                    break;
            }
        }

        arguments.Add(expression.Substring(argumentStart, expression.Length - 1 - argumentStart).Trim());
        return true;
    }

    /// <summary>
    /// Reduces every generic argument list in an expression to its unbound form, so a
    /// constructed type and its definition compare equal.
    /// </summary>
    /// <param name="typeExpression">The expression to reduce.</param>
    /// <returns>
    /// The expression with arguments emptied — <c>Foo&lt;int&gt;</c> becomes
    /// <c>Foo&lt;&gt;</c> and <c>Bar&lt;int, string&gt;</c> becomes <c>Bar&lt;,&gt;</c>. A
    /// non-generic expression is returned unchanged.
    /// </returns>
    internal static string DefinitionKey(string typeExpression)
    {
        if (typeExpression.IndexOf('<') < 0)
        {
            return typeExpression;
        }

        var sb = new StringBuilder(typeExpression.Length);
        var depth = 0;
        var topLevelCommas = 0;

        foreach (var c in typeExpression)
        {
            switch (c)
            {
                case '<':
                    if (depth == 0)
                    {
                        sb.Append('<');
                        topLevelCommas = 0;
                    }

                    depth++;
                    break;
                case '>':
                    depth--;

                    if (depth == 0)
                    {
                        // The arguments themselves were skipped; what identifies the
                        // definition is how many there were.
                        sb.Append(',', topLevelCommas).Append('>');
                    }

                    break;
                case ',' when depth == 1:
                    topLevelCommas++;
                    break;
                default:
                    if (depth == 0)
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        return sb.ToString();
    }
}
