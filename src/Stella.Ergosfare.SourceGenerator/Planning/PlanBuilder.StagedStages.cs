using System.Collections.Immutable;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;
internal sealed partial class PlanBuilder
{
    /// <summary>
    ///     The participant's staged construction expression, or <c>null</c> when keyed
    ///     resolutions are needed but the keyed-service extensions are not resolvable in
    ///     the consuming compilation.
    /// </summary>
    private static string? GatedConstructionExpression(RegistrableTypeModel participant, bool hasKeyedServiceExtensions)
        => participant.StagedConstructionUsesKeyedServices && !hasKeyedServiceExtensions
            ? null
            : participant.StagedConstructionExpression;

    private static bool TryAssembleStagedStages(
        RegistrableTypeModel message,
        List<RegistrableTypeModel> types,
        string? resultTypeExpression,
        bool resultIsValueType,
        bool hasKeyedServiceExtensions,
        PlanGroupFilter filter,
        out ImmutableArray<StagedCallModel> preCalls,
        out ImmutableArray<StagedCallModel> postCalls,
        out ImmutableArray<StagedCallModel> exceptionCalls,
        out ImmutableArray<StagedCallModel> finalCalls)
    {
        preCalls = postCalls = exceptionCalls = finalCalls = ImmutableArray<StagedCallModel>.Empty;

        // The pipeline result the arms match against: the declared result for result
        // pipelines, Unit for void ones — a reference type, so a void pipeline is on the
        // variance-bearing side of the checks below just like a class-typed result.
        var pipelineResultExpression = resultTypeExpression ?? EmittedExpressions.Unit;
        var pipelineResultIsValueType = resultTypeExpression is not null && resultIsValueType;

        var stages = new List<(RegistrableTypeModel Type, StagedCallArm Arm, bool Direct, string? ExceptionFilter, string? Guard)>?[4];

        foreach (var candidate in types)
        {
            if (candidate.ContractShapes.IsEmpty)
            {
                continue;
            }

            for (var kindIndex = 0; kindIndex < 4; kindIndex++)
            {
                var kind = (DescriptorKind)(kindIndex + 1);

                // Deduped registrations of this candidate that reach the message —
                // mirrors the descriptor builders' first-wins (message, result) dedupe,
                // where result-agnostic async contracts carry `object`.
                string? matchedMessageKey = null;
                var matchedDirect = false;
                var registrationCount = 0;
                HashSet<string>? seenRegistrations = null;

                foreach (var shape in candidate.ContractShapes)
                {
                    if (shape.Kind != kind)
                    {
                        continue;
                    }

                    var direct = shape.MessageTypeExpression == message.TypeofExpression;

                    if (!direct && !message.AssignableKeys.Contains(shape.MessageTypeExpression))
                    {
                        continue;
                    }

                    var dedupeKey = shape.MessageTypeExpression + "\x1f"
                        + (kind == DescriptorKind.PreInterceptor
                            ? string.Empty
                            : shape.IsResultTyped ? shape.ResultTypeExpression : "object");

                    if ((seenRegistrations ??= new HashSet<string>(StringComparer.Ordinal)).Add(dedupeKey))
                    {
                        registrationCount++;
                        matchedMessageKey = shape.MessageTypeExpression;
                        matchedDirect = direct;
                    }
                }

                if (registrationCount == 0)
                {
                    continue;
                }

                // More than one registration would put the type into the stage more than
                // once; the order among them is not worth modeling — disqualify.
                if (registrationCount > 1)
                {
                    return false;
                }

                // Out of this plan's group: the set it is keyed by does not select this
                // participant, so the stage simply does not carry it. The filtering plan
                // carries it behind a guard instead.
                if (!filter.TryInclude(candidate, out var candidateGuard))
                {
                    continue;
                }

                // Participation established. The participant itself must be modelable.
                if (!candidate.IsAccessible
                    || !candidate.DiscoveryKeys.IsEmpty
                    || candidate.IsNestedType)
                {
                    return false;
                }

                if (!TrySelectArm(candidate, kind, message, pipelineResultExpression, pipelineResultIsValueType,
                        out var arm, out var exceptionFilter))
                {
                    return false;
                }

                (stages[kindIndex] ??= []).Add((candidate, arm, matchedDirect, exceptionFilter, candidateGuard));
                _ = matchedMessageKey;
            }
        }

        preCalls = OrderStage(stages[0], hasKeyedServiceExtensions);
        postCalls = OrderStage(stages[1], hasKeyedServiceExtensions);
        exceptionCalls = OrderStage(stages[2], hasKeyedServiceExtensions);
        finalCalls = OrderStage(stages[3], hasKeyedServiceExtensions);
        return true;
    }

    /// <summary>
    ///     Selects the pattern-match arm the runtime invoker would take for the
    ///     interceptor, or fails when none resolves (the runtime would throw
    ///     <c>NotSupportedException</c> — the strategy fallback preserves that) or when a
    ///     reference-typed pipeline result meets an inexactly-typed contract (possible
    ///     runtime result variance the string model cannot decide).
    /// </summary>
    private static bool TrySelectArm(
        RegistrableTypeModel candidate,
        DescriptorKind kind,
        RegistrableTypeModel message,
        string pipelineResultExpression,
        bool pipelineResultIsValueType,
        out StagedCallArm arm,
        out string? exceptionFilter)
    {
        arm = default;
        exceptionFilter = null;

        var hasAsyncTyped = false;
        var hasAsyncAgnostic = false;
        var hasSync = false;

        foreach (var shape in candidate.ContractShapes)
        {
            if (shape.Kind != kind)
            {
                continue;
            }

            // A filter the string model cannot reproduce takes the whole plan down: an
            // emitted call with no guard runs an interceptor that declined the exception,
            // and — worse — makes the stage count it as having handled one.
            if (shape.HasUndecidableExceptionFilter)
            {
                return false;
            }

            exceptionFilter = shape.ExceptionFilterExpression;

            // Message-side variance: exact match always works; a base/interface
            // registration matches only for reference-typed messages.
            var messageMatches = shape.MessageTypeExpression == message.TypeofExpression
                || (!message.IsValueType && message.AssignableKeys.Contains(shape.MessageTypeExpression));

            if (!messageMatches)
            {
                continue;
            }

            if (shape.IsResultTyped)
            {
                // Exact result match always works. An `object`-typed contract (the
                // flavored marker interfaces' shape) matches any reference-typed result
                // through the runtime's `in TResult` variance; value-typed results have
                // no variance, so the contract is simply invisible to the pattern match.
                if (shape.ResultTypeExpression == pipelineResultExpression
                    || (!pipelineResultIsValueType && shape.ResultTypeExpression == "object"))
                {
                    if (shape.IsAsync)
                    {
                        hasAsyncTyped = true;
                    }
                    else
                    {
                        hasSync = true;
                    }
                }
                else if (!pipelineResultIsValueType)
                {
                    // Any other base-of relationship the variance could admit is
                    // undecidable in the string model — disqualify.
                    return false;
                }
            }
            else if (shape.IsAsync)
            {
                hasAsyncAgnostic = true;
            }
            else
            {
                // Sync pre carries no result typing.
                hasSync = true;
            }
        }

        if (kind == DescriptorKind.PreInterceptor)
        {
            if (hasAsyncAgnostic)
            {
                arm = StagedCallArm.AsyncAgnostic;
                return true;
            }

            if (hasSync)
            {
                arm = StagedCallArm.Sync;
                return true;
            }

            return false;
        }

        if (hasAsyncTyped)
        {
            arm = StagedCallArm.AsyncTyped;
            return true;
        }

        if (hasAsyncAgnostic)
        {
            arm = StagedCallArm.AsyncAgnostic;
            return true;
        }

        if (hasSync)
        {
            arm = StagedCallArm.Sync;
            return true;
        }

        return false;
    }

    private static ImmutableArray<StagedCallModel> OrderStage(
        List<(RegistrableTypeModel Type, StagedCallArm Arm, bool Direct, string? ExceptionFilter, string? Guard)>? entries,
        bool hasKeyedServiceExtensions)
    {
        if (entries is null)
        {
            return ImmutableArray<StagedCallModel>.Empty;
        }

        entries.Sort(static (x, y) =>
        {
            var bySegment = y.Direct.CompareTo(x.Direct);

            if (bySegment != 0)
            {
                return bySegment;
            }

            var byWeight = y.Type.Weight.CompareTo(x.Type.Weight);

            return byWeight != 0
                ? byWeight
                : string.CompareOrdinal(x.Type.DisplayName, y.Type.DisplayName);
        });

        var calls = ImmutableArray.CreateBuilder<StagedCallModel>(entries.Count);

        foreach (var (type, arm, _, exceptionFilter, guard) in entries)
        {
            calls.Add(new StagedCallModel(
                type.TypeofExpression, arm, GatedConstructionExpression(type, hasKeyedServiceExtensions),
                exceptionFilter, guard));
        }

        return calls.MoveToImmutable();
    }
}
