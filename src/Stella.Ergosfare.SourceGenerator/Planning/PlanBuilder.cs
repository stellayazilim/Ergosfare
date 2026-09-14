using System.Collections.Immutable;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;

/// <summary>
/// Turns discovered types into executable plans and their composition descriptors.
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
/// One class, because these describe a single question — what this compilation's
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
    /// Every supported pipeline uses the same executable plan family, with or without
    /// interceptors or plugin calls.
    /// </remarks>
    internal PlanSet Build()
    {
        // What the planner saw and could not plan. Every disqualification a dispatch cannot
        // survive lands here instead of in a bare return.
        var findings = new List<PlanFinding>();

        var stagedPlans = availability.PlanRegistryHasStagedPlans
            ? ComputeStagedPlans(findings, types, excludedShadows, availability.HasKeyedServiceExtensions,
                defaultResultAdapter is { IsBakeable: true } ? defaultResultAdapter : null,
                pluginInvocations,
                CollectGroupSets(dispatchSites, referencedDispatchSites),
                CollectUnprovableGroupKeys(dispatchSites, referencedDispatchSites))
            : (IReadOnlyList<StagedPlanModel>)Array.Empty<StagedPlanModel>();

        if (availability.PlanRegistryHasStreamPlans)
        {
            var streamPlans = ComputeStreamPlans(findings, types, excludedShadows, availability.HasKeyedServiceExtensions);

            if (streamPlans.Count > 0)
            {
                var combined = new List<StagedPlanModel>(stagedPlans.Count + streamPlans.Count);
                combined.AddRange(stagedPlans);
                combined.AddRange(streamPlans);
                stagedPlans = combined;
            }
        }

        var pipelineDescriptors = availability.PlanRegistryHasPipelineDescriptors
            ? ComputePipelineDescriptors(types, excludedShadows)
            : (IReadOnlyList<PipelineDescriptorModel>)Array.Empty<PipelineDescriptorModel>();

        return new PlanSet(stagedPlans, pipelineDescriptors, findings);
    }

}
