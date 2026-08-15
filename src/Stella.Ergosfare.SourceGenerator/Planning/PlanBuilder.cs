using System.Collections.Immutable;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;

/// <summary>
///     Turns the judged type list into the four plan families the emitter writes: the
///     frozen compositions, the staged pipeline plans, and the two single-handler plan
///     kinds. One class because they are four answers to one question — what does this
///     compilation's dispatch look like — and they have to agree; one file per
///     responsibility because the answers are computed in very different ways.
/// </summary>
/// <param name="types">
///     The judged, name-sorted types emission proceeds with. Order is load-bearing: it is
///     what makes the emitted file deterministic.
/// </param>
/// <param name="excludedShadows">
///     Types hidden from discovery. They contribute pipeline rows — a hidden handler is
///     registered by hand, not absent — without being registered themselves.
/// </param>
internal sealed partial class PlanBuilder(
    List<RegistrableTypeModel> types,
    List<RegistrableTypeModel> excludedShadows,
    ModuleBuilderAvailability availability,
    DefaultResultAdapterSiteModel? defaultResultAdapter,
    ImmutableArray<PluginInvocationModel> pluginInvocations,
    ImmutableArray<DispatchSiteModel> dispatchSites,
    ImmutableArray<DispatchSiteModel> referencedDispatchSites)
{
    /// <summary>
    ///     Every family the referenced package can host, reconciled: a message a plugin
    ///     pulled into the staged family leaves the single-handler one, because the executor
    ///     checks staged plans first and a second plan for that message would be emitted,
    ///     validated at registration, and never reached.
    /// </summary>
    internal PlanSet Build()
    {
        var voidPlans = availability.DispatchRootsHasVoidPlans
            ? ComputeVoidPlans(types)
            : (IReadOnlyList<VoidPlanModel>)Array.Empty<VoidPlanModel>();

        var resultPlans = availability.DispatchRootsHasResultPlans
            ? ComputeResultPlans(types)
            : (IReadOnlyList<ResultPlanModel>)Array.Empty<ResultPlanModel>();

        var stagedPlans = availability.DispatchRootsHasStagedPlans
            ? ComputeStagedPlans(types, excludedShadows, availability.HasKeyedServiceExtensions,
                defaultResultAdapter is { IsBakeable: true } ? defaultResultAdapter : null,
                pluginInvocations,
                CollectGroupSets(dispatchSites, referencedDispatchSites),
                CollectUnprovableGroupKeys(dispatchSites, referencedDispatchSites))
            : (IReadOnlyList<StagedPlanModel>)Array.Empty<StagedPlanModel>();

        if (!pluginInvocations.IsEmpty && stagedPlans.Count > 0)
        {
            var stagedMessages = new HashSet<string>(StringComparer.Ordinal);

            foreach (var plan in stagedPlans)
            {
                stagedMessages.Add(plan.MessageTypeExpression);
            }

            voidPlans = WithoutMessages(voidPlans, stagedMessages, static plan => plan.MessageTypeExpression);
            resultPlans = WithoutMessages(resultPlans, stagedMessages, static plan => plan.MessageTypeExpression);
        }

        var frozenCompositions = availability.DispatchRootsHasFrozenCompositions
            ? ComputeFrozenCompositions(types, excludedShadows)
            : (IReadOnlyList<FrozenCompositionModel>)Array.Empty<FrozenCompositionModel>();

        return new PlanSet(voidPlans, resultPlans, stagedPlans, frozenCompositions);
    }

    /// <summary>The plans whose message is not in the given set, without copying when none is.</summary>
    private static IReadOnlyList<TPlan> WithoutMessages<TPlan>(
        IReadOnlyList<TPlan> plans, HashSet<string> excluded, Func<TPlan, string> messageOf)
    {
        List<TPlan>? kept = null;

        for (var i = 0; i < plans.Count; i++)
        {
            if (!excluded.Contains(messageOf(plans[i])))
            {
                kept?.Add(plans[i]);
                continue;
            }

            if (kept is null)
            {
                kept = new List<TPlan>(plans.Count);

                for (var j = 0; j < i; j++)
                {
                    kept.Add(plans[j]);
                }
            }
        }

        return kept ?? plans;
    }
}
