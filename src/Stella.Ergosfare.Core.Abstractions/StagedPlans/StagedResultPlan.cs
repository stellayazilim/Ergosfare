namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>
/// The result-producing counterpart of <see cref="StagedVoidPlan"/>: a compile-time
/// staged pipeline plan for a message with a result contract. The same advisory contract
/// applies — see <see cref="StagedVoidPlan"/>.
/// </summary>
public abstract class StagedResultPlan
{
    /// <summary>The pipeline composition the plan was baked against.</summary>
    public abstract StagedPlanKey Composition { get; }

    /// <inheritdoc cref="StagedVoidPlan.SupportsDirectConstruction"/>
    public virtual bool SupportsDirectConstruction => false;

    /// <summary>Invokes the visitor with this plan's message and result types as the generic arguments.</summary>
    public abstract TReturn Accept<TReturn, TState>(IStagedResultPlanVisitor<TReturn, TState> visitor, TState state);

    /// <summary>
    /// The union of the groups this plan bakes a filter for, or <c>null</c> when the plan is
    /// keyed by one set and needs no filter. A group-filtering plan serves a dispatch whose
    /// set is a runtime value: it carries every participant and decides per call, so what the
    /// gate must validate is the composition over exactly these groups.
    /// </summary>
    public virtual string[]? FilterGroups => null;

}
