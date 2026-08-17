using System.Collections.Immutable;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;
internal sealed partial class PlanBuilder
{
    /// <summary>
    /// Builds each dispatchable message's whole pipeline: every stage's participants, in the
    /// order they will run.
    /// </summary>
    /// <param name="types">The discovered types.</param>
    /// <param name="excludedShadows">The types hidden from discovery.</param>
    /// <returns>One composition per message that has any participant at all.</returns>
    /// <remarks>
    /// <para>
    /// A participant is direct when its registration names this exact message type and
    /// indirect when it names a type the message is assignable to — the same split the
    /// runtime makes. Each segment is sorted by descending weight and then by type name, and
    /// every row carries the participant's groups.
    /// </para>
    /// <para>
    /// Keyed participants get rows like any other: which rows an application runs is decided
    /// by what it registered, and its catalog narrows the table to exactly that.
    /// </para>
    /// <para>
    /// A message's <c>[ExcludeFromPipeline]</c> is resolved here rather than at dispatch.
    /// Without groups it drops every covariantly matched interceptor; with them it drops only
    /// those carrying a named group. Neither form touches directly registered interceptors or
    /// main handlers.
    /// </para>
    /// <para>
    /// A participant generated code cannot name contributes no row — no registration could
    /// reference it either, so its absence here matches what the container already sees. Two
    /// rows with equal weight and equal name are the same type reached twice, so the order
    /// discovery happened to produce settles it, unobservably.
    /// </para>
    /// </remarks>
    private static List<FrozenCompositionModel> ComputeFrozenCompositions(
        List<RegistrableTypeModel> types, List<RegistrableTypeModel> excludedShadows)
    {
        var compositions = new List<FrozenCompositionModel>();
        // Ten segments: a direct and an indirect one for each of the five stages, indexed by
        // stage and reused across messages.
        var rows = new List<(uint Weight, string SortKey, FrozenParticipantModel Row)>?[10];

        // Messages hidden from discovery still get entries. Being hidden keeps a type out of
        // bulk registration; it does not stop a handler being written for it, and without an
        // entry that message — and every message resolving through it — would have no
        // pipeline at all. Participants are included the same way: a hidden interceptor is
        // one registered by hand, and a container's catalog admits its row only if that
        // container registered it.
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
                // Sorted once here so that consuming a composition never has to.
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

            // A message nothing participates in needs no entry: having none is what tells
            // the runtime it has no pipeline.
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

    /// <summary>
    /// Walks both lists in order.
    /// </summary>
    /// <param name="types">The discovered types.</param>
    /// <param name="excludedShadows">The types hidden from discovery.</param>
    /// <returns>Every type in both, without building a combined list.</returns>
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
    /// Reports whether a message's <c>[ExcludeFromPipeline]</c> keeps a covariantly matched
    /// interceptor out of its pipeline.
    /// </summary>
    /// <param name="message">The message carrying the exclusion.</param>
    /// <param name="interceptor">The interceptor to test.</param>
    /// <returns><c>true</c> when the interceptor is excluded.</returns>
    /// <remarks>
    /// Declared without groups the exclusion covers every covariant interceptor; with groups
    /// it covers only those carrying one of them. An interceptor declaring no groups counts
    /// as carrying the default group alone.
    /// </remarks>
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

    /// <summary>
    /// Reports whether a message is assignable to the type a registration names.
    /// </summary>
    /// <param name="message">The message to test.</param>
    /// <param name="declaredKey">The registered message type, normalized.</param>
    /// <returns><c>true</c> when the registration covariantly matches this message.</returns>
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
