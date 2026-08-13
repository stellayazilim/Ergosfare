namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;
/// <summary>
/// A compile-time staged pipeline plan for a void message: bespoke code that runs the
/// message's entire interceptor-bearing pipeline — pre stages, handler, post stages, with
/// exception and final semantics — as straight-line typed calls instead of the runtime
/// strategy's generic machinery. See <see cref="DispatchRoots.MessageRoot"/> for the visitor re-entry
/// pattern.
/// </summary>
/// <remarks>
/// The plan is advisory: the hosting executor validates <see cref="Composition"/> against
/// the container's selected frozen composition and falls back to the general strategy on
/// a mismatch, so a stale plan only loses its speedup, never changes behavior.
/// <see cref="StagedVoidPlan{TMessage}.Execute"/> must
/// resolve every participant from the provider it is handed — that is exactly what the
/// runtime handler references do outside memoized mode, which the executor's gate
/// excludes.
/// </remarks>
public abstract class StagedVoidPlan
{
    /// <summary>The pipeline composition the plan was baked against.</summary>
    public abstract StagedPlanKey Composition { get; }

    /// <summary>
    /// Whether the plan carries a direct-construction variant of its pipeline
    /// (<c>ExecuteDirect</c>): every participant constructed with <c>new</c> instead of a
    /// container resolution. The hosting executor uses that variant only after verifying
    /// at runtime that every participant's effective DI registration is the module's own
    /// plain transient one — the single shape where container resolution and direct
    /// construction are observably identical.
    /// </summary>
    public virtual bool SupportsDirectConstruction => false;

    /// <summary>Invokes the visitor with this plan's message type as the generic argument.</summary>
    public abstract TReturn Accept<TReturn, TState>(IStagedVoidPlanVisitor<TReturn, TState> visitor, TState state);

    /// <summary>
    /// Runs the plan for a caller that cannot name its message type. The broadcast lane is
    /// the one such caller: a publish is generic over a type constrained only to
    /// <c>notnull</c> — a plain POCO can be an event — so it cannot spell
    /// <see cref="StagedVoidPlan{TMessage}"/> even when the plan it found is exactly that.
    /// </summary>
    /// <remarks>
    /// The cast back is a reference conversion for the types this is reached with: a plan
    /// only exists for a message the generator modeled, and the caller looked it up by that
    /// message's own runtime type. Kept internal — it is a bridge for the library's own
    /// dispatch surfaces, not an entry point.
    /// </remarks>
    internal virtual ValueTask ExecuteErased(
        object message, ErgosfareContext context, IServiceProvider serviceProvider, bool direct)
        => throw new NotSupportedException(
            $"'{GetType()}' does not derive from StagedVoidPlan<TMessage> and carries no erased entry.");
}
