using System.Collections.Immutable;
using Stella.Ergosfare.SourceGenerator.Models;
using Stella.Ergosfare.SourceGenerator.ResultAdapters;

namespace Stella.Ergosfare.SourceGenerator.Planning;
internal sealed partial class PlanBuilder
{
    private static List<StagedPlanModel> ComputeStagedPlans(
        List<RegistrableTypeModel> types,
        List<RegistrableTypeModel> excludedShadows,
        bool hasKeyedServiceExtensions,
        DefaultResultAdapterSiteModel? defaultResultAdapter,
        ImmutableArray<PluginInvocationModel> pluginInvocations,
        Dictionary<string, List<ImmutableArray<string>>> groupSetsByKey,
        HashSet<string> unprovableGroupKeys)
    {
        CollectPipelineFacts(types, out var handlersByMessage, out _);

        var plans = new List<StagedPlanModel>();
        var targetSets = new List<ImmutableArray<string>>();

        foreach (var type in types)
        {
            if (!type.IsDispatchableMessage || type.HasPipelineExclusion)
            {
                continue;
            }

            // The default set always, then every filter a call site proved for this message
            // — through its own type or through a base the site was typed as.
            targetSets.Clear();
            targetSets.Add(ImmutableArray<string>.Empty);
            CollectTargetSets(type, groupSetsByKey, targetSets);

            foreach (var targetGroups in targetSets)
            {
                AddStagedPlan(type, new PlanGroupFilter(targetGroups, filtering: false));
            }

            // And, when a call site names a filter this compilation cannot read, the one plan
            // that answers any set: every participant present, each call behind its guard.
            if (HasUnprovableGroupSite(type, unprovableGroupKeys))
            {
                AddStagedPlan(type, new PlanGroupFilter(ImmutableArray<string>.Empty, filtering: true));
            }
        }

        return plans;

        void AddStagedPlan(RegistrableTypeModel type, PlanGroupFilter filter)
        {
            var targetGroups = filter.Target;

            string? resultTypeExpression = null;
            var resultIsValueType = false;
            var isBroadcast = false;

            if (type.IsCommand && type.DispatchResults.Length == 0)
            {
                // Void pipeline.
            }
            else if (type.IsEvent && type.DispatchResults.Length == 0)
            {
                // Broadcast pipeline: a resultless pipeline like the void one, differing in
                // one thing — every matched handler runs instead of a sole one.
                isBroadcast = true;
            }
            else if ((type.IsCommand || type.IsQuery)
                     && type.DispatchResults.Length == 1
                     && !type.DispatchResults[0].IsStream)
            {
                resultTypeExpression = type.DispatchResults[0].ResultTypeExpression;
                resultIsValueType = type.DispatchResults[0].ResultIsValueType;
            }
            else
            {
                return;
            }

            ImmutableArray<StagedHandlerModel> handlers;
            ImmutableArray<StagedHandlerModel> indirectHandlers;

            if (isBroadcast)
            {
                // No sole-handler gate and no covariant disqualification: a broadcast is
                // where a covariant handler is a legitimate participant rather than a
                // competing claim on the message.
                if (!TryAssembleBroadcastHandlers(type, types, hasKeyedServiceExtensions, filter,
                        out handlers, out indirectHandlers))
                {
                    return;
                }
            }
            else
            {
                // The sole handler gate mirrors the single-handler plans, minus the
                // interceptor suppression (interceptors are the whole point here) — and
                // taken within this plan's own group set, which is the only set that
                // decides which handlers the pipeline it bakes actually has.
                //
                // A send delivers to one handler; under a set that selects none, the
                // pipeline has no handler at all and there is nothing to bake — the runtime
                // lane raises the no-handler outcome the caller asked for.
                if (!TrySelectPlanHandler(type, handlersByMessage, filter, out var handler, out var handlerDescriptor))
                {
                    return;
                }

                if (!handler.IsAccessible || !handler.DiscoveryKeys.IsEmpty)
                {
                    return;
                }

                // The covariant siblings. None of them runs — the ladder gives the message
                // to its direct handler outright — but they are in the live pipeline, and
                // the gate compares the composition a plan was baked against segment by
                // segment. Carrying them is what lets a message with a handler on one of
                // its base contracts keep a plan at all.
                if (!TryCollectCovariantMainHandlers(type, types, excludedShadows, hasKeyedServiceExtensions,
                        filter, out indirectHandlers))
                {
                    return;
                }

                // Selection above already proved the handler belongs to this plan's set;
                // the call is what names the guard a filtering plan's body evaluates.
                if (!filter.TryInclude(handler, out var handlerGuard))
                {
                    return;
                }

                var expectedHandlerResult = resultTypeExpression is null
                    ? EmittedExpressions.ValueTask
                    : EmittedExpressions.ValueTask + "<" + resultTypeExpression + ">";

                if (handlerDescriptor.ResultTypeExpression != expectedHandlerResult
                    || handlerDescriptor.MessageTypeExpression != type.TypeofExpression)
                {
                    return;
                }

                handlers = ImmutableArray.Create(new StagedHandlerModel(
                    handler.TypeofExpression, GatedConstructionExpression(handler, hasKeyedServiceExtensions),
                    handlerGuard));
            }

            if (!TryAssembleStagedStages(type, types, resultTypeExpression, resultIsValueType, hasKeyedServiceExtensions,
                    filter, out var pre, out var post, out var exceptionCalls, out var finalCalls))
            {
                return;
            }

            var pluginCalls = SelectPluginCalls(pluginInvocations, type);

            if (pre.Length + post.Length + exceptionCalls.Length + finalCalls.Length == 0
                && pluginCalls.IsEmpty
                && !isBroadcast
                && targetGroups.IsEmpty)
            {
                // No interceptors and no plugin: the single-handler plans already cover this
                // shape. A plugin is what pulls an interceptorless pipeline in here — its
                // observer has to be emitted into both plan families, and a plan body is the
                // only place a call can live. The body collapses accordingly: with no pre
                // chain, the pipeline start and the pre-handler boundary are the same point,
                // as are the post-handler and after-post ones.
                //
                // A broadcast has no single-handler family to fall back on, so its bare
                // loop IS the plan: the interceptorless publish gets the same straight-line
                // body — and, through the construction gate, the same direct construction —
                // that the interceptorless command already enjoys. Without this arm the
                // flagship "hooks cost zero while not attached" lane is the only lane left
                // resolving its participants through the container per publish.
                //
                // A filtered dispatch has no such family either: the single-handler plans
                // are keyed by message alone and answer only the default set, so leaving a
                // grouped pipeline out here would leave every grouped dispatch — however
                // simple — resolving through the container.
                return;
            }

            // The runtime binding's compile-time mirror: the opt-out suppresses every
            // tier; else the annotation when it fits the slot exactly, else the native
            // carriers, else the compilation's discovered default adapter, else nothing.
            // A fitting annotation the plan cannot bake (inaccessible or uninstantiable
            // adapter) disqualifies the plan — the runtime mirror serves the pipeline
            // instead. A default the discovery could not model (opaque callsite,
            // disagreeing sites, unbakeable type) bakes nothing: a slot it binds at
            // runtime then fails the hosting executor's adapter-identity gate and stays
            // on the strategy.
            var adapterKind = StagedResultAdapterKind.None;
            string? adapterTypeExpression = null;
            var adapterMaterializes = false;

            if (type.HasIgnoredResultAdapter)
            {
                // Classic emission; the runtime binding resolves to null for every tier.
            }
            else if (resultTypeExpression is not null)
            {
                if (type.ResultAdapter is { } annotation && annotation.Fits(resultTypeExpression))
                {
                    if (!annotation.IsBakeable)
                    {
                        return;
                    }

                    adapterKind = StagedResultAdapterKind.Custom;
                    adapterTypeExpression = annotation.TypeofExpression;
                    adapterMaterializes = annotation.Materializes(resultTypeExpression);
                }
                else if (NativeResultAdapters.TryGetExpression(resultTypeExpression, out adapterTypeExpression))
                {
                    adapterKind = StagedResultAdapterKind.Native;
                    adapterMaterializes = true;
                }
                else if (defaultResultAdapter is not null
                         && new DefaultResultAdapterBinder(defaultResultAdapter).TryBind(resultTypeExpression,
                             out adapterTypeExpression, out adapterMaterializes))
                {
                    adapterKind = StagedResultAdapterKind.Custom;
                }
            }
            else if (type.ResultAdapter is { } voidAnnotation && voidAnnotation.Fits(EmittedExpressions.Unit))
            {
                // A Unit-fitting annotation binds the void lane at runtime; void plans do
                // not model adapters, so the plan is disqualified rather than diverging.
                return;
            }
            else if (!type.HasIgnoredResultAdapter
                     && defaultResultAdapter is not null
                     && new DefaultResultAdapterBinder(defaultResultAdapter).TryBind(EmittedExpressions.Unit, out _, out _))
            {
                // A Unit-serving default binds every void lane at runtime; same posture.
                return;
            }

            plans.Add(new StagedPlanModel(
                type.TypeofExpression,
                filter.Filtering ? filter.CoveredGroups : targetGroups,
                isBroadcast,
                resultTypeExpression,
                resultIsValueType,
                handlers,
                indirectHandlers,
                pre, post, exceptionCalls, finalCalls,
                adapterKind, adapterTypeExpression, adapterMaterializes,
                pluginCalls,
                filter.Guards));
        }
    }

    /// <summary>
    ///     Orders one staged stage exactly like the runtime shape builder: the direct
    ///     segment first, then the indirect one, each sorted by weight descending with the
    ///     type name as the ordinal tie-break (participants are non-nested and
    ///     non-generic, so the display name equals the runtime <c>Type.FullName</c>).
    /// </summary>
    /// <summary>
    ///     Assembles a broadcast plan's two handler segments — directly registered handlers
    ///     first, then the covariantly matched ones — in the runtime's own execution order
    ///     (weight-descending, then ordinal by display name). Fails, leaving the message to
    ///     the runtime strategy, when any part of the handler set cannot be modeled exactly.
    /// </summary>
    /// <remarks>
    ///     The covariant segment is the reason a broadcast needs its own assembly step: for a
    ///     single-handler pipeline a covariant handler is a competing claim that disqualifies
    ///     the plan, while here it is an ordinary participant the publish delivers to.
    /// </remarks>
    private static bool TryAssembleBroadcastHandlers(
        RegistrableTypeModel message,
        List<RegistrableTypeModel> types,
        bool hasKeyedServiceExtensions,
        PlanGroupFilter filter,
        out ImmutableArray<StagedHandlerModel> handlers,
        out ImmutableArray<StagedHandlerModel> indirectHandlers)
    {
        handlers = ImmutableArray<StagedHandlerModel>.Empty;
        indirectHandlers = ImmutableArray<StagedHandlerModel>.Empty;

        List<(RegistrableTypeModel Type, bool Direct, string? Guard)>? entries = null;

        foreach (var candidate in types)
        {
            if (candidate.Descriptors.IsEmpty)
            {
                continue;
            }

            var matched = 0;
            var direct = false;

            foreach (var descriptor in candidate.Descriptors)
            {
                if (descriptor.Kind != DescriptorKind.MainHandler)
                {
                    continue;
                }

                var isDirect = descriptor.MessageTypeExpression == message.TypeofExpression;

                if (!isDirect && !message.AssignableKeys.Contains(descriptor.MessageTypeExpression))
                {
                    continue;
                }

                // Only the asynchronous resultless contract is modeled. The runtime also
                // dispatches the ValueTask-shaped and synchronous ones through a type
                // switch; a plan would have to reproduce that choice per handler, and the
                // strategy already does it correctly.
                if (descriptor.ResultTypeExpression != EmittedExpressions.ValueTask)
                {
                    return false;
                }

                matched++;
                direct = isDirect;
            }

            if (matched == 0)
            {
                continue;
            }

            // Reached through two registrations — the handler would appear in the pipeline
            // more than once and the order among the appearances is not worth modeling.
            if (matched > 1)
            {
                return false;
            }

            // Out of this plan's group, so not a participant of it — and not a defect
            // either: the set this plan is keyed by simply does not select it. The filtering
            // plan takes everyone instead and remembers the test each one runs behind.
            if (!filter.TryInclude(candidate, out var candidateGuard))
            {
                continue;
            }

            if (!candidate.IsAccessible
                || !candidate.DiscoveryKeys.IsEmpty
                || candidate.IsNestedType)
            {
                return false;
            }

            (entries ??= []).Add((candidate, direct, candidateGuard));
        }

        if (entries is null)
        {
            return false;
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

        var directBuilder = ImmutableArray.CreateBuilder<StagedHandlerModel>();
        var indirectBuilder = ImmutableArray.CreateBuilder<StagedHandlerModel>();

        foreach (var (type, isDirect, guard) in entries)
        {
            var model = new StagedHandlerModel(
                type.TypeofExpression, GatedConstructionExpression(type, hasKeyedServiceExtensions), guard);

            (isDirect ? directBuilder : indirectBuilder).Add(model);
        }

        handlers = directBuilder.ToImmutable();
        indirectHandlers = indirectBuilder.ToImmutable();
        return true;
    }

    /// <summary>
    ///     The covariantly matched main handlers of a message, in the order the frozen
    ///     composition holds them — weight-descending, then ordinal by metadata name, the
    ///     rule <see cref="ComputeFrozenCompositions"/> applies to the very same segment.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     None of these handlers runs. The settled priority ladder gives the message to its
    ///     direct handler no matter how many covariant candidates exist — a covariant handler
    ///     is a fallback, not a competitor — and both pipeline bodies implement exactly that.
    ///     They are collected because the staged gate compares every segment of the live
    ///     composition against the baked one, so a plan claiming an empty covariant segment
    ///     would be refused by every pipeline that has a base-contract handler.
    ///     </para>
    ///     <para>
    ///     Returns <c>false</c> when a participant cannot be modeled: a keyed registration,
    ///     whose presence depends on which container was built, or a type reached through two
    ///     registrations, whose appearances the segment cannot order. An inaccessible type is
    ///     not in the frozen composition either, so it is skipped rather than disqualifying.
    ///     </para>
    /// </remarks>
    private static bool TryCollectCovariantMainHandlers(
        RegistrableTypeModel message,
        List<RegistrableTypeModel> types,
        List<RegistrableTypeModel> excludedShadows,
        bool hasKeyedServiceExtensions,
        PlanGroupFilter filter,
        out ImmutableArray<StagedHandlerModel> indirectHandlers)
    {
        indirectHandlers = ImmutableArray<StagedHandlerModel>.Empty;

        var messageKey = TypeExpressions.DefinitionKey(message.TypeofExpression);
        List<(uint Weight, string SortKey, StagedHandlerModel Model)>? rows = null;

        foreach (var candidate in Enumerate(types, excludedShadows))
        {
            if (candidate.Descriptors.IsEmpty || !candidate.IsAccessible)
            {
                continue;
            }

            var matched = 0;
            var covariant = false;

            foreach (var descriptor in candidate.Descriptors)
            {
                if (descriptor.Kind != DescriptorKind.MainHandler)
                {
                    continue;
                }

                var declaredKey = TypeExpressions.DefinitionKey(descriptor.MessageTypeExpression);
                var direct = declaredKey == messageKey;

                if (!direct && !ContainsAssignableKey(message, declaredKey))
                {
                    continue;
                }

                matched++;
                covariant = !direct;
            }

            // The direct handler is the plan's own, and a type nothing matched is not in
            // this pipeline at all.
            if (matched == 0 || (matched == 1 && !covariant))
            {
                continue;
            }

            if (matched > 1 || !candidate.DiscoveryKeys.IsEmpty)
            {
                return false;
            }

            // Out of this plan's group set, so not in the composition it is baked against.
            if (!filter.TryInclude(candidate, out var guard))
            {
                continue;
            }

            (rows ??= []).Add((
                candidate.Weight,
                candidate.MetadataSortKey,
                new StagedHandlerModel(
                    candidate.TypeofExpression,
                    GatedConstructionExpression(candidate, hasKeyedServiceExtensions),
                    guard)));
        }

        if (rows is null)
        {
            return true;
        }

        rows.Sort(static (x, y) =>
        {
            var byWeight = y.Weight.CompareTo(x.Weight);

            return byWeight != 0 ? byWeight : string.CompareOrdinal(x.SortKey, y.SortKey);
        });

        var builder = ImmutableArray.CreateBuilder<StagedHandlerModel>(rows.Count);

        foreach (var row in rows)
        {
            builder.Add(row.Model);
        }

        indirectHandlers = builder.MoveToImmutable();

        return true;
    }
}
