using System.Collections.Immutable;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;

/// <summary>
/// Turns the discovered types into what the emitter writes: the compiled compositions, the
/// staged plans, and the two kinds of single-handler plan.
/// </summary>
/// <param name="types">
/// The types to proceed with, sorted by name. The order matters: it is what makes the
/// generated file come out the same every build.
/// </param>
/// <param name="excludedShadows">
/// Types hidden from discovery. They still contribute pipeline rows — a hidden handler is
/// one registered by hand, not one that is absent — but are not registered themselves.
/// </param>
/// <param name="availability">Which surfaces the referenced packages can host.</param>
/// <param name="defaultResultAdapter">
/// The application's fallback result adapter, when one was configured.
/// </param>
/// <param name="pluginInvocations">The plugin methods to write calls to.</param>
/// <param name="dispatchSites">The dispatch sites found in this compilation.</param>
/// <param name="referencedDispatchSites">The dispatch sites recorded by referenced assemblies.</param>
/// <remarks>
/// One class, because these are four answers to a single question — what this compilation's
/// dispatch looks like — and they have to agree with each other. One file per part, because
/// the answers are worked out in very different ways.
/// </remarks>
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
    /// Builds every family the referenced packages can host, reconciled with one another.
    /// </summary>
    /// <returns>The complete set of plans and compositions to write.</returns>
    /// <remarks>
    /// A message a plugin pulled into the staged family leaves the single-handler one: the
    /// executor looks for a staged plan first, so a second plan for that message would be
    /// written, checked at registration, and never used.
    /// </remarks>
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

    /// <summary>
    /// Returns the plans whose message is not in the given set.
    /// </summary>
    /// <typeparam name="TPlan">The kind of plan being filtered.</typeparam>
    /// <param name="plans">The plans to filter.</param>
    /// <param name="excluded">The messages to drop.</param>
    /// <param name="messageOf">Reads a plan's message type.</param>
    /// <returns>
    /// The surviving plans, or the original list itself when nothing was dropped — which is
    /// the usual case and copies nothing.
    /// </returns>
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

            // The first drop is what forces a copy; everything kept before it is carried
            // over here, and everything after goes through the branch above.
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
