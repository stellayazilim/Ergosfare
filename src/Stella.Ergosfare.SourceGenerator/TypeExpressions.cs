using System.Text;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
///     Pure string operations on the type expressions the models carry — the
///     <c>global::</c>-qualified spellings the generated file uses. Nothing here reads a
///     symbol; a type expression is the only input, which is what lets the plan layer
///     compare types it never resolved.
/// </summary>
internal static class TypeExpressions
{
    private const string GlobalPrefix = "global::";

    /// <summary>Drops the <c>global::</c> qualifier when present.</summary>
    internal static string StripGlobalPrefix(string typeExpression)
        => typeExpression.StartsWith(GlobalPrefix, StringComparison.Ordinal)
            ? typeExpression.Substring(GlobalPrefix.Length)
            : typeExpression;

    /// <summary>
    ///     Splits <c>Name&lt;A, B&lt;C&gt;&gt;</c> into the base name and its top-level
    ///     argument expressions; <c>false</c> for non-generic expressions (including
    ///     shapes the splitter does not model, such as tuples and arrays of generics —
    ///     those compare textually).
    /// </summary>
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
                        // A '>' closing the outer list before the end: not a plain
                        // Name<...> shape (e.g. "X<T>.Nested") — compare textually.
                        return false;
                    }

                    break;
                case ',' when depth == 1:
                    arguments.Add(expression.Substring(argumentStart, i - argumentStart).Trim());
                    argumentStart = i + 1;
                    break;
            }
        }

        arguments.Add(expression.Substring(argumentStart, expression.Length - 1 - argumentStart).Trim());
        return true;
    }

    /// <summary>
    ///     Reduces every generic argument list in a type expression to its unbound form
    ///     (<c>Foo&lt;int&gt;</c> → <c>Foo&lt;&gt;</c>, <c>Bar&lt;int, string&gt;</c> →
    ///     <c>Bar&lt;,&gt;</c>), so constructed and definition spellings compare equal.
    ///     Non-generic expressions pass through unchanged.
    /// </summary>
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
