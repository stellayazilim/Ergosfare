namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>
/// <see cref="StagedBroadcastPlan"/> closed over its event type; generated plans derive
/// from this.
/// </summary>
/// <typeparam name="TEvent">The event this plan serves.</typeparam>
/// <remarks>
/// The type parameter is constrained to <c>notnull</c> rather than <see cref="IMessage"/>
/// because a plain object can be an event and a publish is generic over that same
/// constraint. That lets the publishing path name this type directly, where reaching a void
/// plan from a publish would need an untyped step.
/// </remarks>
public abstract class StagedBroadcastPlan<TEvent> : StagedBroadcastPlan
    where TEvent : notnull
{
    /// <summary>
    /// Runs the compiled pipeline for <paramref name="message"/>, delivering it to every
    /// handler the plan was compiled with.
    /// </summary>
    /// <param name="message">The event to publish.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <param name="serviceProvider">
    /// The provider participants are resolved from — the publishing scope's.
    /// </param>
    /// <returns>A task that completes when every handler has run.</returns>
    /// <remarks>
    /// Only called while the live pipeline still matches
    /// <see cref="StagedBroadcastPlan.Composition"/>.
    /// </remarks>
    public abstract ValueTask Execute(TEvent message, ErgosfareContext context, IServiceProvider serviceProvider);

    /// <summary>
    /// Runs the compiled pipeline with participants constructed directly rather than
    /// resolved.
    /// </summary>
    /// <param name="message">The event to publish.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <param name="serviceProvider">
    /// The provider participants' own dependencies are resolved from.
    /// </param>
    /// <returns>A task that completes when every handler has run.</returns>
    /// <remarks>
    /// Only called when <see cref="StagedBroadcastPlan.SupportsDirectConstruction"/> is
    /// <c>true</c> and the publishing path has confirmed every participant's plain transient
    /// registration. The default implementation runs <see cref="Execute"/> instead.
    /// </remarks>
    public virtual ValueTask ExecuteDirect(TEvent message, ErgosfareContext context, IServiceProvider serviceProvider)
        => Execute(message, context, serviceProvider);

    /// <summary>
    /// Runs the compiled pipeline for a publish whose groups are only known now, testing
    /// each participant's groups before calling it.
    /// </summary>
    /// <param name="message">The event to publish.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <param name="groups">The groups the publish asked for.</param>
    /// <returns>A task that completes when every matching handler has run.</returns>
    /// <remarks>
    /// Only called on a plan that reports <see cref="StagedBroadcastPlan.FilterGroups"/>.
    /// The default implementation runs the unfiltered body.
    /// </remarks>
    public virtual ValueTask ExecuteFiltered(
        TEvent message, ErgosfareContext context, IServiceProvider serviceProvider, IReadOnlyList<string> groups)
        => Execute(message, context, serviceProvider);

    /// <summary>
    /// Runs <see cref="ExecuteFiltered"/> with participants constructed directly; see
    /// <see cref="ExecuteDirect"/> for when that applies.
    /// </summary>
    /// <param name="message">The event to publish.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <param name="serviceProvider">
    /// The provider participants' own dependencies are resolved from.
    /// </param>
    /// <param name="groups">The groups the publish asked for.</param>
    /// <returns>A task that completes when every matching handler has run.</returns>
    public virtual ValueTask ExecuteFilteredDirect(
        TEvent message, ErgosfareContext context, IServiceProvider serviceProvider, IReadOnlyList<string> groups)
        => ExecuteFiltered(message, context, serviceProvider, groups);

}
