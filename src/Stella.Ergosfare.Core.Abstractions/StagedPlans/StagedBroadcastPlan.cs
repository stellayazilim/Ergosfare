namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>
/// A compile-time staged pipeline plan for a broadcast: bespoke code that runs a published
/// message's entire pipeline — pre stages, every matched handler in order, post stages, with
/// exception and final semantics — as straight-line typed calls instead of the runtime
/// machinery.
/// </summary>
/// <remarks>
/// <para>
/// Its own family rather than a shape of the resultless plan, even though the emitted body is
/// the same one with a loop where the other has a call. Keeping them apart means nothing ever
/// has to ask which kind of pipeline it is holding: a publish looks here, a send looks at the
/// resultless plans, and the delivery difference is settled by which store answered rather
/// than by a branch on the message. It is also where a delivery shape — sequential today,
/// possibly a declared parallel one later — can be expressed without touching the
/// single-handler families.
/// </para>
/// <para>
/// The plan is advisory: the publishing lane validates <see cref="Composition"/> against the
/// container's live pipeline and falls back to the runtime delivery on a mismatch, so a stale
/// plan only loses its speedup, never changes behavior.
/// </para>
/// </remarks>
public abstract class StagedBroadcastPlan
{
    /// <summary>The pipeline composition the plan was baked against.</summary>
    public abstract StagedPlanKey Composition { get; }

    /// <summary>
    /// Whether the plan carries a direct-construction variant of its pipeline
    /// (<c>ExecuteDirect</c>): every participant constructed with <c>new</c> instead of a
    /// container resolution. The publishing lane uses that variant only after verifying at
    /// runtime that every participant's effective DI registration is the module's own plain
    /// transient one — the single shape where container resolution and direct construction
    /// are observably identical.
    /// </summary>
    public virtual bool SupportsDirectConstruction => false;

    /// <summary>
    /// The union of the groups this plan bakes a filter for, or <c>null</c> when the plan is
    /// keyed by one set and needs no filter. A group-filtering plan serves a dispatch whose
    /// set is a runtime value: it carries every participant and decides per call, so what the
    /// gate must validate is the composition over exactly these groups.
    /// </summary>
    public virtual string[]? FilterGroups => null;

}
