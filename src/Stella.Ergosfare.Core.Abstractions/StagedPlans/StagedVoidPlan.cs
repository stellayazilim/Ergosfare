namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;
/// <summary>
/// A compiled plan that runs a void message's whole pipeline — pre-interceptors, handler,
/// post-interceptors, and the exception and final behavior around them — as straight-line
/// typed calls rather than through the general strategy.
/// </summary>
/// <remarks>
/// The plan is a proposal: the executor compares <see cref="Composition"/> with the
/// pipeline this container actually selected and falls back to the general strategy if they
/// differ, so an out-of-date plan costs its speedup and nothing else. Its
/// <see cref="StagedVoidPlan{TMessage}.Execute"/> must resolve every participant from the
/// provider it is given, which is what the general path does too outside memoized mode —
/// and memoized pipelines never reach a plan.
/// </remarks>
public abstract class StagedVoidPlan
{
    /// <summary>
    /// The pipeline this plan was compiled against.
    /// </summary>
    public abstract StagedPlanKey Composition { get; }

    /// <summary>
    /// Whether the plan can also run with every participant constructed directly instead of
    /// resolved from the container.
    /// </summary>
    /// <remarks>
    /// The executor takes that route only after confirming that every participant's
    /// registration is the module's own plain transient one — the one case where
    /// constructing and resolving cannot be told apart.
    /// </remarks>
    public virtual bool SupportsDirectConstruction => false;

    /// <summary>
    /// Calls <paramref name="visitor"/> with this plan's message type as its generic
    /// argument.
    /// </summary>
    /// <typeparam name="TReturn">What the visitor produces.</typeparam>
    /// <typeparam name="TState">The state the visitor needs.</typeparam>
    /// <param name="visitor">The visitor to call.</param>
    /// <param name="state">Passed to the visitor unchanged.</param>
    /// <returns>Whatever the visitor produced.</returns>
    public abstract TReturn Accept<TReturn, TState>(IStagedVoidPlanVisitor<TReturn, TState> visitor, TState state);

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
