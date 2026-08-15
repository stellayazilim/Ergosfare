using System.Collections.Immutable;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;
internal sealed partial class PlanBuilder
{
    /// <summary>
    ///     The plugin methods emitted into one plan: those whose family filter admits the
    ///     message's module, whose discovery-key filter admits the message's keys, and whose
    ///     generic constraints the message satisfies. A method failing any of them
    ///     contributes nothing to this plan — no call, no runtime check — which is the
    ///     design's point about a constraint being the filter.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Pipeline shape is not among the filters: no hook carries a result, so one
    ///         declaration serves a void command, a result-producing query and a broadcast
    ///         alike. The one family outside this path is the stream lane, which has no plan
    ///         to emit into — so a plugin declaring <c>Module.Query</c> covers a query's
    ///         single-result dispatches and not its streaming ones.
    ///     </para>
    ///     <para>
    ///         The order is ordinal by service type then method name. Weight-by-registration
    ///         order is a property of the consumer's fluent chain, which this slice does not
    ///         read; a stable arbitrary order is preferable to an unstable one, and pinning it
    ///         here keeps the emitted source deterministic.
    ///     </para>
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

    private static bool MatchesModule(PluginModule modules, RegistrableTypeModel message)
        => (message.IsCommand && (modules & PluginModule.Command) != 0)
           || (message.IsQuery && (modules & PluginModule.Query) != 0)
           || (message.IsEvent && (modules & PluginModule.Event) != 0);

    /// <summary>
    ///     The key filter. An unwritten one selects the default key alone — the same set
    ///     <c>RegisterGenerated()</c> without a pattern selects. A keyed message was opted
    ///     out of default discovery by its author, and a plugin the consumer installed
    ///     without naming a key should not quietly opt it back in.
    /// </summary>
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
    ///     Whether the message satisfies the method's message-parameter constraints, decided
    ///     against the same assignable chain the covariant interceptor match uses.
    /// </summary>
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
