using System.Collections.Immutable;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;
internal sealed partial class PlanBuilder
{
    /// <summary>
    ///     Computes the frozen pipeline compositions: for every dispatchable message, the
    ///     full six-stage participant table in the runtime shape-builder's exact execution
    ///     order — direct/indirect split at the seam the runtime descriptor split uses
    ///     (declared message equality vs assignability), each segment sorted
    ///     weight-descending then ordinal CLR <c>FullName</c>, group labels baked per row.
    ///     The table is the dispatch authority, so it carries what the registry used to.
    ///     Keyed participants get rows like any other — which of them an application runs
    ///     is settled by what it registered, and the consuming catalog narrows the table
    ///     to exactly that. A message's <c>[ExcludeFromPipeline]</c> is resolved here:
    ///     the blanket form drops every covariantly matched interceptor, the group-scoped
    ///     form drops the covariant interceptors carrying an excluded group, and neither
    ///     touches directly registered interceptors or main handlers.
    /// </summary>
    /// <remarks>
    ///     A participant generated code cannot name (inaccessible) contributes no row:
    ///     no registration surface can reference it either, so its absence from the table
    ///     is the same absence the container already sees. An exact comparator tie —
    ///     equal weight and equal metadata name, i.e. the same type reached twice — is
    ///     broken by discovery order, which is deterministic and, the participants being
    ///     identical, unobservable.
    /// </remarks>
    private static List<FrozenCompositionModel> ComputeFrozenCompositions(
        List<RegistrableTypeModel> types, List<RegistrableTypeModel> excludedShadows)
    {
        var compositions = new List<FrozenCompositionModel>();
        var rows = new List<(uint Weight, string SortKey, FrozenParticipantModel Row)>?[10];

        // Messages hidden from discovery still get entries: [ExcludeFromDiscovery] keeps a
        // type out of bulk registration, it does not stop a handler from being written for
        // it — and without an entry such a message (and every subtype resolving through it)
        // would have no pipeline at all. Participants are widened the same way: a hidden
        // interceptor is registered by hand, and the consuming catalog admits its row only
        // for the container that did register it.
        foreach (var message in Enumerate(types, excludedShadows))
        {
            if (!message.IsMessageShape)
            {
                continue;
            }

            var messageKey = TypeExpressions.DefinitionKey(message.TypeofExpression);
            Array.Clear(rows, 0, rows.Length);

            foreach (var candidate in Enumerate(types, excludedShadows))
            {
                if (candidate.Descriptors.IsEmpty || !candidate.IsAccessible)
                {
                    continue;
                }

                foreach (var descriptor in candidate.Descriptors)
                {
                    var declaredKey = TypeExpressions.DefinitionKey(descriptor.MessageTypeExpression);
                    var direct = declaredKey == messageKey;

                    if (!direct && !ContainsAssignableKey(message, declaredKey))
                    {
                        continue;
                    }

                    if (!direct
                        && descriptor.Kind != DescriptorKind.MainHandler
                        && IsExcludedFromPipeline(message, candidate))
                    {
                        continue;
                    }

                    var segment = (int)descriptor.Kind * 2 + (direct ? 0 : 1);
                    (rows[segment] ??= []).Add((
                        candidate.Weight,
                        candidate.MetadataSortKey,
                        new FrozenParticipantModel(
                            candidate.TypeofExpression, candidate.GroupsExpression)));
                }
            }

            var segments = new ImmutableArray<FrozenParticipantModel>[10];
            var isEmpty = true;

            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i] is not { Count: > 0 } segmentRows)
                {
                    segments[i] = ImmutableArray<FrozenParticipantModel>.Empty;
                    continue;
                }

                isEmpty = false;
                segmentRows.Sort(static (x, y) =>
                {
                    var byWeight = y.Weight.CompareTo(x.Weight);
                    return byWeight != 0 ? byWeight : string.CompareOrdinal(x.SortKey, y.SortKey);
                });

                var builder = ImmutableArray.CreateBuilder<FrozenParticipantModel>(segmentRows.Count);

                foreach (var row in segmentRows)
                {
                    builder.Add(row.Row);
                }

                segments[i] = builder.MoveToImmutable();
            }

            if (isEmpty)
            {
                continue;
            }

            compositions.Add(new FrozenCompositionModel(
                message.TypeofExpression,
                segments[0], segments[1], segments[2], segments[3], segments[4],
                segments[5], segments[6], segments[7], segments[8], segments[9]));
        }

        return compositions;
    }

    /// <summary>Both model lists in order, without materializing a combined one.</summary>
    private static IEnumerable<RegistrableTypeModel> Enumerate(
        List<RegistrableTypeModel> types, List<RegistrableTypeModel> excludedShadows)
    {
        foreach (var type in types)
        {
            yield return type;
        }

        foreach (var shadow in excludedShadows)
        {
            yield return shadow;
        }
    }

    /// <summary>
    ///     Whether the message's <c>[ExcludeFromPipeline]</c> keeps a covariantly matched
    ///     interceptor out of its pipeline: the parameterless form excludes every one, the
    ///     group-scoped form only those carrying a named group. Mirrors the runtime
    ///     shape-builder's <c>PrepareIndirect</c>, including its treatment of an
    ///     interceptor without <c>[Group]</c> as carrying the default group alone.
    /// </summary>
    private static bool IsExcludedFromPipeline(RegistrableTypeModel message, RegistrableTypeModel interceptor)
    {
        if (!message.HasPipelineExclusion)
        {
            return false;
        }

        if (message.ExcludedInterceptorGroups.IsEmpty)
        {
            return true;
        }

        foreach (var excluded in message.ExcludedInterceptorGroups)
        {
            if (interceptor.GroupNames.IsEmpty)
            {
                if (excluded == GroupNames.Default)
                {
                    return true;
                }

                continue;
            }

            foreach (var group in interceptor.GroupNames)
            {
                if (string.Equals(group, excluded, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsAssignableKey(RegistrableTypeModel message, string declaredKey)
    {
        foreach (var assignableKey in message.AssignableKeys)
        {
            if (TypeExpressions.DefinitionKey(assignableKey) == declaredKey)
            {
                return true;
            }
        }

        return false;
    }
}
