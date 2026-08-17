namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>
/// A compiled plan that runs a result-producing message's whole pipeline as straight-line
/// typed calls; the result-producing counterpart of <see cref="StagedVoidPlan"/>, with the
/// same rules about when it is trusted.
/// </summary>
public abstract class StagedResultPlan
{
    /// <summary>
    /// The pipeline this plan was compiled against.
    /// </summary>
    public abstract StagedPlanKey Composition { get; }

    /// <inheritdoc cref="StagedVoidPlan.SupportsDirectConstruction"/>
    public virtual bool SupportsDirectConstruction => false;

    /// <summary>
    /// Calls <paramref name="visitor"/> with this plan's message and result types as its
    /// generic arguments.
    /// </summary>
    /// <typeparam name="TReturn">What the visitor produces.</typeparam>
    /// <typeparam name="TState">The state the visitor needs.</typeparam>
    /// <param name="visitor">The visitor to call.</param>
    /// <param name="state">Passed to the visitor unchanged.</param>
    /// <returns>Whatever the visitor produced.</returns>
    public abstract TReturn Accept<TReturn, TState>(IStagedResultPlanVisitor<TReturn, TState> visitor, TState state);

    /// <inheritdoc cref="StagedVoidPlan.FilterGroups"/>
    public virtual string[]? FilterGroups => null;

}
