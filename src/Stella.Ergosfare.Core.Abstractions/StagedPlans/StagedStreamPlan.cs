namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>
/// A compiled plan that runs a streaming query's whole pipeline — pre-interceptors, the
/// stream handler, the enumeration, and the post-, exception- and final stages around it —
/// as straight-line typed calls.
/// </summary>
/// <remarks>
/// Like its void and result siblings, the plan is verified against the live pipeline before
/// it runs, and its <see cref="StagedStreamPlan{TQuery, TResult}.Execute"/> must resolve
/// every participant from the provider it is given.
/// </remarks>
public abstract class StagedStreamPlan
{
    /// <summary>
    /// The pipeline this plan was compiled against.
    /// </summary>
    public abstract StagedPlanKey Composition { get; }

    /// <summary>
    /// Calls <paramref name="visitor"/> with this plan's query and item types as its
    /// generic arguments.
    /// </summary>
    /// <typeparam name="TReturn">What the visitor produces.</typeparam>
    /// <typeparam name="TState">The state the visitor needs.</typeparam>
    /// <param name="visitor">The visitor to call.</param>
    /// <param name="state">Passed to the visitor unchanged.</param>
    /// <returns>Whatever the visitor produced.</returns>
    public abstract TReturn Accept<TReturn, TState>(IStagedStreamPlanVisitor<TReturn, TState> visitor, TState state);
}
