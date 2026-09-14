namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;
/// <summary>
/// A compiled plan that runs a void message's whole pipeline — pre-interceptors, handler,
/// post-interceptors, and the exception and final behavior around them — as straight-line
/// typed calls.
/// </summary>
/// <remarks>
/// The plan is the executor. Its descriptor is checked against registration when the
/// engine is initialized. Its one generated body constructs eligible parameterless
/// participants and resolves injected participants from the caller's scope. A descriptor
/// mismatch fails dispatch instead of constructing another execution path.
/// </remarks>
public abstract class StagedVoidPlan : ICompiledPlan
{
    /// <summary>
    /// The pipeline this plan was compiled against.
    /// </summary>
    public abstract StagedPlanKey Composition { get; }

    /// <summary>
    /// Every group this plan can filter for, or <c>null</c> when the plan was compiled for
    /// one group set and needs no filtering.
    /// </summary>
    /// <remarks>
    /// A filtering plan serves dispatches whose groups are only known at runtime: it holds
    /// every participant and decides per call, so what the executor must check is the
    /// pipeline over exactly these groups.
    /// </remarks>
    public virtual string[]? FilterGroups => null;

}
