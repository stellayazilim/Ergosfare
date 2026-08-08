namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// A compile-time staged pipeline plan for a void message: bespoke code that runs the
/// message's entire interceptor-bearing pipeline — pre stages, handler, post stages, with
/// exception and final semantics — as straight-line typed calls instead of the runtime
/// strategy's generic machinery. See <see cref="MessageRoot"/> for the visitor re-entry
/// pattern.
/// </summary>
/// <remarks>
/// The plan is advisory: the hosting executor re-validates <see cref="Composition"/>
/// against the live registry on every version change and falls back to the runtime
/// strategy whenever the pipeline no longer matches, so a stale plan only loses its
/// speedup, never changes behavior. <see cref="StagedVoidPlan{TMessage}.Execute"/> must
/// resolve every participant from the provider it is handed — that is exactly what the
/// runtime handler references do outside memoized mode, which the executor's gate
/// excludes.
/// </remarks>
public abstract class StagedVoidPlan
{
    /// <summary>The pipeline composition the plan was baked against.</summary>
    public abstract StagedPlanComposition Composition { get; }

    /// <summary>Invokes the visitor with this plan's message type as the generic argument.</summary>
    public abstract TReturn Accept<TReturn, TState>(IStagedVoidPlanVisitor<TReturn, TState> visitor, TState state);
}
