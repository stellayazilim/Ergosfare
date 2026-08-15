using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.ResultAdapters;

/// <summary>
///     Answers what the container's default result adapter binds for one result slot — the
///     closed adapter type and whether it also materializes. A closed adapter answers by
///     slot lookup; an open one unifies its carrier patterns with the slot and closes over
///     the bindings.
/// </summary>
/// <remarks>
///     Both the plan layer and the unserved-slot diagnostic ask this, and they must agree:
///     a plan baking an adapter the diagnostic considers unserved (or the reverse) is the
///     two-answers-for-one-question shape the generator already has too much of.
/// </remarks>
internal sealed class DefaultResultAdapterBinder(DefaultResultAdapterSiteModel adapter)
{
    /// <summary>
    ///     The adapter serving this slot, or <c>false</c> when the default adapter does not
    ///     serve it at all.
    /// </summary>
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

            if (!matcher.TryMatch(pattern, resultTypeExpression, bindings)
                || Array.IndexOf(bindings, null) >= 0)
            {
                continue;
            }

            adapterTypeExpression = adapter.BaseTypeExpression + "<" + string.Join(", ", bindings) + ">";

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
