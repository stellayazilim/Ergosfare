using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.ResultAdapters;

/// <summary>
/// Works out what the application's fallback adapter binds for one result type.
/// </summary>
/// <param name="adapter">The configured fallback adapter.</param>
/// <remarks>
/// Both the planning layer and the unserved-result diagnostic ask here, and they have to
/// agree: a plan compiled against an adapter the diagnostic thinks serves nothing — or the
/// other way round — would be two answers to one question.
/// </remarks>
internal sealed class DefaultResultAdapterBinder(DefaultResultAdapterSiteModel adapter)
{
    /// <summary>
    /// Finds the adapter that serves a result type.
    /// </summary>
    /// <param name="resultTypeExpression">The result type to serve.</param>
    /// <param name="adapterTypeExpression">
    /// The closed adapter type when this method returns <c>true</c>; otherwise <c>null</c>.
    /// </param>
    /// <param name="materializes">
    /// Whether that adapter can also build the result type from a failure. Meaningful only
    /// when this method returns <c>true</c>.
    /// </param>
    /// <returns><c>true</c> when the fallback adapter serves the result type.</returns>
    /// <remarks>
    /// A closed adapter is answered by looking the result type up among the ones it serves.
    /// An open definition is matched against each of its declared patterns, and the first
    /// that fits is closed over what the match bound.
    /// </remarks>
    internal bool TryBind(string resultTypeExpression, out string? adapterTypeExpression, out bool materializes)
    {
        adapterTypeExpression = null;
        materializes = false;

        if (!adapter.IsOpenGeneric)
        {
            if (!AdapterSlotKey.Contains(adapter.AdapterSlotsKey, resultTypeExpression))
            {
                return false;
            }

            adapterTypeExpression = adapter.BaseTypeExpression;
            materializes = AdapterSlotKey.Contains(adapter.MaterializerSlotsKey, resultTypeExpression);
            return true;
        }

        var matcher = new TypePatternMatcher(adapter.ParameterNamesKey.Split(AdapterSlotKey.Separator));

        foreach (var pattern in AdapterSlotKey.Split(adapter.AdapterSlotsKey))
        {
            var bindings = new string?[adapter.Arity];

            // A pattern that matches but leaves a parameter unbound cannot be closed, so it
            // does not serve this result type either.
            if (!matcher.TryMatch(pattern, resultTypeExpression, bindings)
                || Array.IndexOf(bindings, null) >= 0)
            {
                continue;
            }

            adapterTypeExpression = adapter.BaseTypeExpression + "<" + string.Join(", ", bindings) + ">";

            // Whether it also builds the result type is asked of the same bindings: the
            // pattern has to render back to exactly this result type.
            foreach (var materializerPattern in AdapterSlotKey.Split(adapter.MaterializerSlotsKey))
            {
                if (matcher.Render(materializerPattern, bindings) == resultTypeExpression)
                {
                    materializes = true;
                    break;
                }
            }

            return true;
        }

        return false;
    }
}
