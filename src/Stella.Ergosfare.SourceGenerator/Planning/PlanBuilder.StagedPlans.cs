using System.Collections.Immutable;
using Stella.Ergosfare.SourceGenerator.Models;
using Stella.Ergosfare.SourceGenerator.ResultAdapters;

namespace Stella.Ergosfare.SourceGenerator.Planning;
internal sealed partial class PlanBuilder
{
    /// <summary>
    /// Builds the staged plans: one compiled pipeline body per message and group set.
    /// </summary>
    /// <param name="types">The discovered types.</param>
    /// <param name="excludedShadows">The types hidden from discovery.</param>
    /// <param name="hasKeyedServiceExtensions">
    /// Whether the consuming compilation can resolve the keyed-service extensions.
    /// </param>
    /// <param name="defaultResultAdapter">The container's default result adapter, if it names one.</param>
    /// <param name="pluginInvocations">The plugin methods visible to this compilation.</param>
    /// <param name="groupSetsByKey">The group sets dispatch sites name, per message key.</param>
    /// <param name="unprovableGroupKeys">The messages named under a set that could not be read.</param>
    /// <returns>The plans, in discovery order.</returns>
    /// <remarks>
    /// A staged plan runs a fixed list of calls in a fixed order, so it is built only for a
    /// message whose whole pipeline is settled here — handler, interceptors, plugin calls and
    /// result adapter alike. Anything left open drops that one plan and the dispatch keeps
    /// the general path, which is always correct.
    /// <para>
    /// Every message gets a plan for the default set, plus one for each set some dispatch
    /// names for it. A message named under a set this compilation cannot read also gets the
    /// filtering plan, which carries every participant and decides each one at run time.
    /// </para>
    /// </remarks>
    private static List<StagedPlanModel> ComputeStagedPlans(
        List<PlanFinding> findings,
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

            // The default set always, then every set a dispatch names for this message —
            // through its own type, or through a base the site was written against.
            targetSets.Clear();
            targetSets.Add(ImmutableArray<string>.Empty);
            CollectTargetSets(type, groupSetsByKey, targetSets);

            foreach (var targetGroups in targetSets)
            {
                AddStagedPlan(type, new PlanGroupFilter(targetGroups, filtering: false));
            }

            // And, when a dispatch names a set this compilation cannot read, the one plan that
            // answers any set: every participant present, each call behind its own test.
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
                // A broadcast pipeline: resultless like the void one, differing in a single
                // thing — every matched handler runs instead of one.
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
                // No sole-handler test and no covariant disqualification: a broadcast is where
                // a covariant handler is an ordinary participant rather than a competing
                // claim on the message.
                if (!TryAssembleBroadcastHandlers(type, types, hasKeyedServiceExtensions, filter,
                        out handlers, out indirectHandlers))
                {
                    return;
                }
            }
            else
            {
                // The same sole-handler requirement the single-handler plans have, minus their
                // interceptor suppression — interceptors are the whole point here — and asked
                // within this plan's own group set, the only set that decides which handlers
                // the pipeline it bakes actually has.
                //
                // A send delivers to one handler, so under a set selecting none there is no
                // pipeline to bake, and the runtime lane raises the no-handler outcome the
                // caller asked for.
                if (!TrySelectPlanHandler(findings, type, handlersByMessage, filter, out var handler, out var handlerDescriptor))
                {
                    return;
                }

                if (!handler.IsAccessible || !handler.DiscoveryKeys.IsEmpty)
                {
                    return;
                }

                // The covariant siblings. None of them runs — the ladder gives the message to
                // its direct handler outright — but they are in the live pipeline, and the
                // check compares the composition a plan was baked against segment by segment.
                // Carrying them is what lets a message with a handler on one of its base
                // contracts keep a plan at all.
                if (!TryCollectCovariantMainHandlers(type, types, excludedShadows, hasKeyedServiceExtensions,
                        filter, out indirectHandlers))
                {
                    return;
                }

                // Selection above already established that the handler belongs to this plan's
                // set; this call is what names the test a filtering plan's body runs.
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
                // No interceptor and no plugin: the single-handler plans already serve this
                // shape. A plugin is what pulls an interceptorless pipeline in here, since
                // its observer has to reach both plan families and a plan body is the only
                // place a call can live. The body collapses accordingly — with no pre chain,
                // the pipeline start and the pre-handler boundary are one point, as are the
                // post-handler and after-post ones.
                //
                // A broadcast has no single-handler family to fall back on, so its bare loop
                // is the plan: an interceptorless publish gets the same straight-line body,
                // and the same direct construction, an interceptorless send already gets.
                // Without this arm, publishing would be the one lane still resolving its
                // participants through the container every time.
                //
                // A grouped dispatch has no such family either — the single-handler plans are
                // keyed by message alone and answer the default set only — so leaving a
                // grouped pipeline out here would send every grouped dispatch, however
                // simple, through the container.
                return;
            }

            // The same order the runtime binding walks: the opt-out suppresses every tier;
            // otherwise the annotation when it fits the slot exactly, then the native
            // carriers, then this compilation's default adapter, then nothing. A fitting
            // annotation the plan cannot bake — an inaccessible or uninstantiable adapter —
            // drops the plan, and the runtime serves that pipeline instead. A default that
            // could not be read, from an opaque call, disagreeing calls or an unbakeable
            // type, bakes nothing: a slot it binds at run time then fails the executor's
            // adapter-identity check and stays on the general path.
            var adapterKind = StagedResultAdapterKind.None;
            string? adapterTypeExpression = null;
            var adapterMaterializes = false;

            if (type.HasIgnoredResultAdapter)
            {
                // The plain body: the runtime binding answers null for every tier.
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
                // An annotation fitting Unit binds the void lane at run time, and a void plan
                // carries no adapter, so the plan is dropped rather than left to diverge.
                return;
            }
            else if (!type.HasIgnoredResultAdapter
                     && defaultResultAdapter is not null
                     && new DefaultResultAdapterBinder(defaultResultAdapter).TryBind(EmittedExpressions.Unit, out _, out _))
            {
                // A default serving Unit binds every void lane at run time; same answer.
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
    /// Builds a broadcast plan's two handler segments.
    /// </summary>
    /// <param name="message">The event the plan serves.</param>
    /// <param name="types">The discovered types.</param>
    /// <param name="hasKeyedServiceExtensions">
    /// Whether the consuming compilation can resolve the keyed-service extensions.
    /// </param>
    /// <param name="filter">The plan's group set.</param>
    /// <param name="handlers">The directly registered handlers, when this returns <c>true</c>.</param>
    /// <param name="indirectHandlers">
    /// The covariantly matched handlers, when this returns <c>true</c>.
    /// </param>
    /// <returns><c>true</c> when the whole handler set could be settled.</returns>
    /// <remarks>
    /// Both segments come back in the order the runtime runs them: heavier weights first,
    /// then by type name. A broadcast needs its own step because of the covariant segment —
    /// for a single-handler pipeline a covariant handler is a competing claim that drops the
    /// plan, while here it is an ordinary participant the publish delivers to.
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

                // Only the asynchronous resultless contract is called from a plan. The
                // runtime reaches the ValueTask-shaped and synchronous ones through a type
                // switch, which a plan would have to reproduce per handler — and the general
                // path already does it.
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

            // Reached through two registrations: the handler would appear in the pipeline
            // more than once, and the order among those appearances is not worth modeling.
            if (matched > 1)
            {
                return false;
            }

            // Not selected by this plan's set, so not a participant of it — and no defect
            // either. The filtering plan takes everyone instead, remembering the test each
            // one runs behind.
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
    /// Collects a message's covariantly matched main handlers.
    /// </summary>
    /// <param name="message">The message the plan serves.</param>
    /// <param name="types">The discovered types.</param>
    /// <param name="excludedShadows">The types hidden from discovery.</param>
    /// <param name="hasKeyedServiceExtensions">
    /// Whether the consuming compilation can resolve the keyed-service extensions.
    /// </param>
    /// <param name="filter">The plan's group set.</param>
    /// <param name="indirectHandlers">
    /// The covariant handlers, when this returns <c>true</c>; empty when there are none.
    /// </param>
    /// <returns><c>true</c> when the segment could be settled.</returns>
    /// <remarks>
    /// <para>
    /// They come back in the order the frozen composition holds them — heavier weights first,
    /// then by metadata name, the rule <see cref="ComputeFrozenCompositions"/> applies to
    /// this same segment. None of them runs: the priority ladder gives the message to its
    /// direct handler however many covariant candidates there are, and both pipeline bodies
    /// do exactly that. They are collected because the check compares every segment of the
    /// live composition against the baked one, so a plan claiming an empty covariant segment
    /// would be refused by every pipeline that has a base-contract handler.
    /// </para>
    /// <para>
    /// Answers <c>false</c> for a participant that cannot be settled: a keyed registration,
    /// whose presence depends on which container was built, or a type reached through two
    /// registrations, whose appearances the segment cannot order. An inaccessible type is not
    /// in the frozen composition either, so it is skipped rather than taking the plan down.
    /// </para>
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

            // The direct handler is the plan's own, and a type nothing matched is not in this
            // pipeline at all.
            if (matched == 0 || (matched == 1 && !covariant))
            {
                continue;
            }

            if (matched > 1 || !candidate.DiscoveryKeys.IsEmpty)
            {
                return false;
            }

            // Not selected by this plan's set, so not in the composition it is checked
            // against.
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
