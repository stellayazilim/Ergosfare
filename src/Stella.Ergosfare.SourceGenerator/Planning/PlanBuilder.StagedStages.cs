using System.Collections.Immutable;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;
internal sealed partial class PlanBuilder
{
    /// <summary>
    /// Gives the expression that constructs a participant inside a staged plan.
    /// </summary>
    /// <param name="participant">The participant to construct.</param>
    /// <param name="hasKeyedServiceExtensions">
    /// Whether the consuming compilation can resolve the keyed-service extensions.
    /// </param>
    /// <returns>
    /// The construction expression, or <c>null</c> when it would need keyed resolution the
    /// consuming compilation cannot spell — the call then goes through the container.
    /// </returns>
    private static string? GatedConstructionExpression(RegistrableTypeModel participant, bool hasKeyedServiceExtensions)
        => participant.StagedConstructionUsesKeyedServices && !hasKeyedServiceExtensions
            ? null
            : participant.StagedConstructionExpression;

    /// <summary>
    /// Builds the four interceptor stages of one staged plan.
    /// </summary>
    /// <param name="message">The message the plan serves.</param>
    /// <param name="types">The discovered types.</param>
    /// <param name="resultTypeExpression">The pipeline's result, or <c>null</c> for a void pipeline.</param>
    /// <param name="resultIsValueType">Whether that result is a value type.</param>
    /// <param name="hasKeyedServiceExtensions">
    /// Whether the consuming compilation can resolve the keyed-service extensions.
    /// </param>
    /// <param name="filter">The plan's group set.</param>
    /// <param name="preCalls">The pre-interceptor calls, when this returns <c>true</c>.</param>
    /// <param name="postCalls">The post-interceptor calls, when this returns <c>true</c>.</param>
    /// <param name="exceptionCalls">The exception-interceptor calls, when this returns <c>true</c>.</param>
    /// <param name="finalCalls">The final-interceptor calls, when this returns <c>true</c>.</param>
    /// <returns><c>true</c> when every stage could be assembled.</returns>
    /// <remarks>
    /// A single participant whose place in the pipeline the plan cannot pin down takes the
    /// whole plan with it: a staged plan runs a fixed list of calls, so it is built only when
    /// every one of them is settled here. Each stage comes back in the order the runtime
    /// would run it — direct registrations before covariant ones, then by descending weight,
    /// then by type name.
    /// </remarks>
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

        // What the arms match against: the declared result for a result pipeline, Unit for a
        // void one. Unit is a reference type, which puts a void pipeline on the
        // variance-bearing side of the checks below alongside a class-typed result.
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

                // This candidate's registrations that reach the message, deduped the way the
                // descriptor builders do it: first one wins per (message, result), with a
                // result-agnostic async contract counting as `object`.
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

                // More than one registration puts the type into the stage more than once,
                // and the order among those runs is not worth modeling here.
                if (registrationCount > 1)
                {
                    return false;
                }

                // The plan's set does not select this participant, so the stage simply does
                // not carry it. The filtering plan carries it behind a guard instead.
                if (!filter.TryInclude(candidate, out var candidateGuard))
                {
                    continue;
                }

                // It participates; now it must be nameable. A keyed or nested participant is
                // not something the plan can call directly.
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
    /// Selects the contract the runtime invoker would call an interceptor through.
    /// </summary>
    /// <param name="candidate">The interceptor to call.</param>
    /// <param name="kind">The stage it is being called in.</param>
    /// <param name="message">The message being dispatched.</param>
    /// <param name="pipelineResultExpression">The pipeline's result type.</param>
    /// <param name="pipelineResultIsValueType">Whether that result is a value type.</param>
    /// <param name="arm">The selected contract, when this returns <c>true</c>.</param>
    /// <param name="exceptionFilter">
    /// The interceptor's exception filter, when it declares one; <c>null</c> otherwise.
    /// </param>
    /// <returns><c>true</c> when exactly one contract is the answer.</returns>
    /// <remarks>
    /// Fails when no contract resolves — the runtime throws <c>NotSupportedException</c>
    /// there, and letting the plan go keeps that — and when a reference-typed result meets
    /// an inexactly typed contract, where variance decides the answer at run time and type
    /// names alone cannot say what it will be.
    /// </remarks>
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

            // A filter that cannot be reproduced here takes the whole plan down. An emitted
            // call carrying no guard runs an interceptor that declined the exception, and
            // has the stage count it as having handled one.
            if (shape.HasUndecidableExceptionFilter)
            {
                return false;
            }

            exceptionFilter = shape.ExceptionFilterExpression;

            // Message-side variance: an exact match always works, while a registration
            // against a base type or interface matches reference-typed messages only.
            var messageMatches = shape.MessageTypeExpression == message.TypeofExpression
                || (!message.IsValueType && message.AssignableKeys.Contains(shape.MessageTypeExpression));

            if (!messageMatches)
            {
                continue;
            }

            if (shape.IsResultTyped)
            {
                // An exact result match always works. An `object`-typed contract, the shape
                // the flavored marker interfaces have, reaches any reference-typed result
                // through the runtime's `in TResult` variance; a value-typed result has no
                // variance to reach through, so that contract is invisible to it.
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
                    // Any other base-of relationship the variance might admit cannot be
                    // decided from type names alone.
                    return false;
                }
            }
            else if (shape.IsAsync)
            {
                hasAsyncAgnostic = true;
            }
            else
            {
                // A synchronous pre-interceptor carries no result typing at all.
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

    /// <summary>
    /// Puts one stage's participants in the order the runtime would run them.
    /// </summary>
    /// <param name="entries">The stage's participants, or <c>null</c> when it has none.</param>
    /// <param name="hasKeyedServiceExtensions">
    /// Whether the consuming compilation can resolve the keyed-service extensions.
    /// </param>
    /// <returns>The stage's calls, in order.</returns>
    /// <remarks>
    /// Directly registered participants run before covariantly matched ones, then heavier
    /// weights before lighter, and equal weights by type name.
    /// </remarks>
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
