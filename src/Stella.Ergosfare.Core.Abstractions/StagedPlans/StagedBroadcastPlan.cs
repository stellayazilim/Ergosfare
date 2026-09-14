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
/// Like the other plans, this object is directly executable. Registration is checked against
/// <see cref="Composition"/> when the engine is initialized; a mismatch fails dispatch.
/// </para>
/// </remarks>
public abstract class StagedBroadcastPlan : ICompiledPlan
{
    /// <summary>
    /// The pipeline this plan was compiled against.
    /// </summary>
    public abstract StagedPlanKey Composition { get; }

    /// <inheritdoc cref="StagedVoidPlan.FilterGroups"/>
    public virtual string[]? FilterGroups => null;

}
