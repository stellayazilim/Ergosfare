using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;

/// <summary>
///     What one compilation's dispatch compiles to: the frozen composition table plus the
///     three plan families, already reconciled with each other. Every list is empty when the
///     referenced Ergosfare package has no surface to register it against.
/// </summary>
internal sealed record PlanSet(
    IReadOnlyList<VoidPlanModel> VoidPlans,
    IReadOnlyList<ResultPlanModel> ResultPlans,
    IReadOnlyList<StagedPlanModel> StagedPlans,
    IReadOnlyList<FrozenCompositionModel> FrozenCompositions);
