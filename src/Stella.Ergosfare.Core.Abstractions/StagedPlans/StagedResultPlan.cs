namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>
/// The result-producing counterpart of <see cref="StagedVoidPlan"/>: a compile-time
/// staged pipeline plan for a message with a result contract. The same advisory contract
/// applies — see <see cref="StagedVoidPlan"/>.
/// </summary>
public abstract class StagedResultPlan
{
    /// <summary>The pipeline composition the plan was baked against.</summary>
    public abstract StagedPlanComposition Composition { get; }

    /// <summary>Invokes the visitor with this plan's message and result types as the generic arguments.</summary>
    public abstract TReturn Accept<TReturn, TState>(IStagedResultPlanVisitor<TReturn, TState> visitor, TState state);
}
