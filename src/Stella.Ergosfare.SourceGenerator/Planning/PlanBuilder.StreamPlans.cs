using System.Collections.Immutable;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;
internal sealed partial class PlanBuilder
{
    /// <summary>
    /// Builds the stream plans: one compiled pipeline body per (query, item) pair.
    /// </summary>
    /// <param name="findings">The list disqualifications a dispatch cannot survive land in.</param>
    /// <param name="types">The discovered types.</param>
    /// <param name="excludedShadows">The types hidden from discovery.</param>
    /// <param name="hasKeyedServiceExtensions">
    /// Whether the consuming compilation can resolve the keyed-service extensions.
    /// </param>
    /// <returns>The plans, in discovery order.</returns>
    /// <remarks>
    /// <para>
    /// A stream plan is built for the default set only — per-set stream plans do not exist
    /// yet, and the engine refuses a grouped stream outright — so candidacy is the staged
    /// plans' with the streaming differences: the message's result entry is a streamed one,
    /// its sole direct handler claims it through the stream-handler contract, and no
    /// covariant handler claims it at all. The stages around the enumeration are assembled
    /// exactly as for the other plans, closed over the enumerator the stream threads through
    /// them.
    /// </para>
    /// <para>
    /// Unlike a send, a streamless pipeline still gets its plan: streaming has no
    /// single-handler family to fall back on, so the bare enumeration is the plan — without
    /// it, an interceptorless stream would have no lane at all.
    /// </para>
    /// </remarks>
    private static List<StagedPlanModel> ComputeStreamPlans(
        List<PlanFinding> findings,
        List<RegistrableTypeModel> types,
        List<RegistrableTypeModel> excludedShadows,
        bool hasKeyedServiceExtensions)
    {
        CollectPipelineFacts(types, out var handlersByMessage, out _);

        var plans = new List<StagedPlanModel>();

        foreach (var type in types)
        {
            if (!type.IsDispatchableMessage || type.HasPipelineExclusion)
            {
                continue;
            }

            foreach (var dispatchResult in type.DispatchResults)
            {
                if (!dispatchResult.IsStream)
                {
                    continue;
                }

                // The default set is the only set a stream plan serves, and it decides which
                // participants the pipeline it bakes actually has.
                var filter = new PlanGroupFilter(ImmutableArray<string>.Empty, filtering: false);

                // The same sole-handler requirement the staged plans have, asked within the
                // default set. A grouped stream has no plan to find — the engine refuses the
                // dispatch itself — so a handler outside the default set simply leaves this
                // pair unplanned.
                if (!TrySelectPlanHandler(findings, type, handlersByMessage, filter, out var handler, out var handlerDescriptor))
                {
                    continue;
                }

                // It must be nameable, discovered by default, and claim exactly this pair
                // through the stream-handler contract — the one contract a stream plan calls.
                if (!handler.IsAccessible
                    || !handler.DiscoveryKeys.IsEmpty
                    || handler.IsNestedType
                    || handlerDescriptor.ResultTypeExpression
                        != EmittedExpressions.AsyncEnumerable + "<" + dispatchResult.ResultTypeExpression + ">"
                    || handlerDescriptor.MessageTypeExpression != type.TypeofExpression)
                {
                    continue;
                }

                // A covariant claim drops the plan outright. The strategy's fallback to a
                // base-contract handler is not compiled this round, and baking the direct
                // handler over a live pipeline that also holds a covariant one would fail
                // the composition check on every stream.
                if (!TryCollectCovariantMainHandlers(type, types, excludedShadows, hasKeyedServiceExtensions,
                        filter, out var indirectHandlers)
                    || !indirectHandlers.IsEmpty)
                {
                    continue;
                }

                // The stages close over the enumerator: a streaming pipeline's post,
                // exception and final interceptors receive it in their result slot, so the
                // arm selection asks about that type rather than the item's.
                if (!TryAssembleStagedStages(type, types,
                        EmittedExpressions.AsyncEnumerator + "<" + dispatchResult.ResultTypeExpression + ">",
                        resultIsValueType: false, hasKeyedServiceExtensions, filter,
                        out var pre, out var post, out var exceptionCalls, out var finalCalls))
                {
                    continue;
                }

                plans.Add(new StagedPlanModel(
                    type.TypeofExpression,
                    ImmutableArray<string>.Empty,
                    IsBroadcast: false,
                    IsStream: true,
                    dispatchResult.ResultTypeExpression,
                    dispatchResult.ResultIsValueType,
                    ImmutableArray.Create(new StagedHandlerModel(
                        handler.TypeofExpression, GatedConstructionExpression(handler, hasKeyedServiceExtensions))),
                    ImmutableArray<StagedHandlerModel>.Empty,
                    pre, post, exceptionCalls, finalCalls,
                    StagedResultAdapterKind.None, null, false,
                    ImmutableArray<PluginInvocationModel>.Empty,
                    ImmutableArray<StagedGroupGuardModel>.Empty));
            }
        }

        return plans;
    }
}
