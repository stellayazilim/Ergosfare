namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>The typed closure of <see cref="StagedBroadcastPlan"/>; subclassed by generated plans.</summary>
/// <remarks>
/// Constrained to <c>notnull</c> rather than <see cref="IMessage"/> on purpose: a publish is
/// generic over the same constraint because a plain POCO can be an event, so the publishing
/// lane can name this type directly. The resultless plans cannot be named that way, which is
/// why reaching one from a publish needed an erased entry — this family needs none.
/// </remarks>
public abstract class StagedBroadcastPlan<TEvent> : StagedBroadcastPlan
    where TEvent : notnull
{
    /// <summary>
    /// Runs the baked pipeline for the message, delivering to every handler the composition
    /// was baked with. Only invoked while the live pipeline matches
    /// <see cref="StagedBroadcastPlan.Composition"/>; participants resolve from
    /// <paramref name="serviceProvider"/> — the publishing scope's provider.
    /// </summary>
    public abstract ValueTask Execute(TEvent message, ErgosfareContext context, IServiceProvider serviceProvider);

    /// <summary>
    /// The direct-construction variant of <see cref="Execute"/>: participants are constructed
    /// with <c>new</c> (dependencies still resolve from <paramref name="serviceProvider"/>).
    /// Only invoked while <see cref="StagedBroadcastPlan.SupportsDirectConstruction"/> is
    /// <c>true</c> AND the publishing lane verified every participant's plain transient
    /// registration; the default forwards to <see cref="Execute"/>.
    /// </summary>
    public virtual ValueTask ExecuteDirect(TEvent message, ErgosfareContext context, IServiceProvider serviceProvider)
        => Execute(message, context, serviceProvider);
}
