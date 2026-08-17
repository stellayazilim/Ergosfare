using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;

/// <summary>
/// Everything one compilation's dispatch compiles to: the composition table and the three
/// families of plan, already reconciled with one another.
/// </summary>
/// <param name="VoidPlans">Single-handler plans for messages that produce no result.</param>
/// <param name="ResultPlans">Single-handler plans for messages that produce one.</param>
/// <param name="StagedPlans">Plans for pipelines that carry interceptor stages.</param>
/// <param name="FrozenCompositions">The compiled composition of each message type.</param>
/// <remarks>
/// Every list is empty when the referenced Ergosfare package offers nothing to register
/// them against.
/// </remarks>
internal sealed record PlanSet(
    IReadOnlyList<VoidPlanModel> VoidPlans,
    IReadOnlyList<ResultPlanModel> ResultPlans,
    IReadOnlyList<StagedPlanModel> StagedPlans,
    IReadOnlyList<FrozenCompositionModel> FrozenCompositions);
