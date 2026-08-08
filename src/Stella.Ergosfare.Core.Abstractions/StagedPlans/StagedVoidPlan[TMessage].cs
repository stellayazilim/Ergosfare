
namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>The typed closure of <see cref="StagedVoidPlan"/>; subclassed by generated (or hand-written) plans.</summary>
public abstract class StagedVoidPlan<TMessage> : StagedVoidPlan
    where TMessage : IMessage
{
    /// <summary>
    /// Runs the baked pipeline for the message. Only invoked while the live pipeline
    /// matches <see cref="StagedVoidPlan.Composition"/>; participants resolve from
    /// <paramref name="serviceProvider"/> — the dispatching scope's provider.
    /// </summary>
    public abstract ValueTask Execute(TMessage message, IExecutionContext context, IServiceProvider serviceProvider);

    /// <summary>
    /// The direct-construction variant of <see cref="Execute"/>: participants are
    /// constructed with <c>new</c> (dependencies still resolve from
    /// <paramref name="serviceProvider"/>). Only invoked while
    /// <see cref="StagedVoidPlan.SupportsDirectConstruction"/> is <c>true</c> AND the
    /// hosting executor verified every participant's plain transient registration; the
    /// default forwards to <see cref="Execute"/>.
    /// </summary>
    public virtual ValueTask ExecuteDirect(TMessage message, IExecutionContext context, IServiceProvider serviceProvider)
        => Execute(message, context, serviceProvider);

    /// <inheritdoc />
    public sealed override TReturn Accept<TReturn, TState>(IStagedVoidPlanVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage>(state);
}
