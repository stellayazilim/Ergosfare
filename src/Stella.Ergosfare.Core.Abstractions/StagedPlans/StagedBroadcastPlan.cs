namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>
/// A compiled plan that runs a published message's whole pipeline — pre-interceptors, every
/// matched handler in order, post-interceptors, and the exception and final behavior around
/// them — as straight-line typed calls.
/// </summary>
/// <remarks>
/// <para>
/// Broadcast plans are their own family rather than a shape of the void plans, even though
/// the compiled body differs only in having a loop where the other has a call. Keeping them
/// apart means nothing has to ask which kind of pipeline it holds: a publish looks here, a
/// send looks at the void plans, and which store answered settles how delivery works. It is
/// also where a delivery shape other than today's sequential one could be expressed without
/// disturbing the single-handler families.
/// </para>
/// <para>
/// Like the other plans this one is a proposal: the publishing path compares
/// <see cref="Composition"/> with the live pipeline and falls back to the general delivery
/// if they differ, so an out-of-date plan costs its speedup and nothing else.
/// </para>
/// </remarks>
public abstract class StagedBroadcastPlan
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
    /// The publishing path takes that route only after confirming that every participant's
    /// registration is the module's own plain transient one — the one case where
    /// constructing and resolving cannot be told apart.
    /// </remarks>
    public virtual bool SupportsDirectConstruction => false;

    /// <inheritdoc cref="StagedVoidPlan.FilterGroups"/>
    public virtual string[]? FilterGroups => null;

}
