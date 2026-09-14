using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;

/// <summary>
/// The executable plans, composition descriptors and diagnostics for one compilation.
/// </summary>
/// <param name="StagedPlans">Executable plans for all supported pipeline shapes.</param>
/// <param name="PipelineDescriptors">The compiled composition of each message type.</param>
/// <remarks>
/// Every list is empty when the referenced Ergosfare package offers nothing to register
/// them against.
/// </remarks>
internal sealed record PlanSet(
    IReadOnlyList<StagedPlanModel> StagedPlans,
    IReadOnlyList<PipelineDescriptorModel> PipelineDescriptors,
    IReadOnlyList<PlanFinding> Findings);
