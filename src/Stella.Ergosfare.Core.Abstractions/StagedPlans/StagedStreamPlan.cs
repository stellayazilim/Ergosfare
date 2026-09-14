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
public abstract class StagedStreamPlan : ICompiledPlan
{
    /// <summary>
    /// The pipeline this plan was compiled against.
    /// </summary>
    public abstract StagedPlanKey Composition { get; }

}
