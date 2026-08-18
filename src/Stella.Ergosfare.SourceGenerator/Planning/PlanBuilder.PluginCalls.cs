using System.Collections.Immutable;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;
internal sealed partial class PlanBuilder
{
    /// <summary>
    /// Picks the plugin methods to call from one message's plan.
    /// </summary>
    /// <param name="invocations">Every plugin method discovered.</param>
    /// <param name="message">The message whose plan is being built.</param>
    /// <returns>
    /// The methods whose family, key and constraint filters all admit this message, sorted
    /// so the generated file comes out the same every build.
    /// </returns>
    /// <remarks>
    /// <para>
    /// A method failing any filter contributes nothing here — no call and no runtime test.
    /// That is the point of using the constraint as the filter.
    /// </para>
    /// <para>
    /// Pipeline shape is not a filter: no hook carries a result, so one declaration serves a
    /// resultless command, a result-producing query and a broadcast alike. The one family
    /// this path does not reach is streaming, whose plan bodies carry no hook points yet —
    /// so a plugin naming the query family covers a query's single-result dispatches but not
    /// its streaming ones.
    /// </para>
    /// <para>
    /// The order is by service type and then method name. Ordering by registration would
    /// mean reading the consumer's own call chain, which this does not see; a stable
    /// arbitrary order beats an unstable one.
    /// </para>
    /// </remarks>
    private static ImmutableArray<PluginInvocationModel> SelectPluginCalls(
        ImmutableArray<PluginInvocationModel> invocations,
        RegistrableTypeModel message)
    {
        if (invocations.IsEmpty)
        {
            return ImmutableArray<PluginInvocationModel>.Empty;
        }

        ImmutableArray<PluginInvocationModel>.Builder? selected = null;

        foreach (var invocation in invocations)
        {
            if (invocation.Constraints.IsUnmodelable
                || !MatchesModule(invocation.Modules, message)
                || !MatchesDiscoveryKeys(invocation.Keys, message.DiscoveryKeys)
                || !SatisfiesConstraints(invocation.Constraints, message))
            {
                continue;
            }

            // Nothing is allocated until something is selected, which is the usual case for
            // a compilation with no plugins at all.
            (selected ??= ImmutableArray.CreateBuilder<PluginInvocationModel>()).Add(invocation);
        }

        if (selected is null)
        {
            return ImmutableArray<PluginInvocationModel>.Empty;
        }

        selected.Sort(static (x, y) =>
        {
            var byType = string.CompareOrdinal(x.ServiceTypeExpression, y.ServiceTypeExpression);
            return byType != 0 ? byType : string.CompareOrdinal(x.MethodName, y.MethodName);
        });

        return selected.ToImmutable();
    }

    /// <summary>
    /// Reports whether a method's family filter admits a message.
    /// </summary>
    /// <param name="modules">The families the method applies to.</param>
    /// <param name="message">The message to test.</param>
    /// <returns><c>true</c> when the message belongs to one of them.</returns>
    private static bool MatchesModule(PluginModule modules, RegistrableTypeModel message)
        => (message.IsCommand && (modules & PluginModule.Command) != 0)
           || (message.IsQuery && (modules & PluginModule.Query) != 0)
           || (message.IsEvent && (modules & PluginModule.Event) != 0);

    /// <summary>
    /// Reports whether a method's key filter admits a message.
    /// </summary>
    /// <param name="filter">The keys the method applies to; empty means it named none.</param>
    /// <param name="declared">The keys the message declares; empty means it declared none.</param>
    /// <returns><c>true</c> when the two overlap.</returns>
    /// <remarks>
    /// Naming no keys selects the default key alone — the same set a pattern-less
    /// <c>RegisterGenerated()</c> selects. A keyed message was kept out of default discovery
    /// by its author, and a plugin installed without naming a key should not quietly put it
    /// back in.
    /// </remarks>
    private static bool MatchesDiscoveryKeys(ImmutableArray<string> filter, ImmutableArray<string> declared)
    {
        if (filter.IsDefaultOrEmpty)
        {
            return declared.IsDefaultOrEmpty;
        }

        foreach (var key in filter)
        {
            if (declared.IsDefaultOrEmpty)
            {
                // The message carries the default key, which the filter reaches only by
                // naming it explicitly.
                if (key.Length == 0)
                {
                    return true;
                }

                continue;
            }

            foreach (var candidate in declared)
            {
                if (string.Equals(key, candidate, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Reports whether a message satisfies a method's constraints.
    /// </summary>
    /// <param name="constraints">The method's constraints.</param>
    /// <param name="message">The message to test.</param>
    /// <returns><c>true</c> when the method could be closed over this message.</returns>
    /// <remarks>
    /// Decided against the same list of assignable types that covariant interceptor matching
    /// uses.
    /// </remarks>
    private static bool SatisfiesConstraints(PluginConstraintModel constraints, RegistrableTypeModel message)
    {
        if (constraints.RequiresReferenceType && message.IsValueType)
        {
            return false;
        }

        if (constraints.RequiresValueType && !message.IsValueType)
        {
            return false;
        }

        foreach (var required in constraints.MessageTypes)
        {
            if (required == message.TypeofExpression)
            {
                continue;
            }

            if (!message.AssignableKeys.Contains(required))
            {
                return false;
            }
        }

        return true;
    }
}
