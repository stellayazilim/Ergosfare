

namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;
/// <summary>The typed closure of <see cref="StagedResultPlan"/>; subclassed by generated (or hand-written) plans.</summary>
public abstract class StagedResultPlan<TMessage, TResult> : StagedResultPlan
    where TMessage : IMessage
{
    /// <inheritdoc cref="StagedVoidPlan{TMessage}.Execute"/>
    public abstract ValueTask<TResult> Execute(TMessage message, ErgosfareContext context, IServiceProvider serviceProvider);

    /// <inheritdoc cref="StagedVoidPlan{TMessage}.ExecuteDirect"/>
    public virtual ValueTask<TResult> ExecuteDirect(TMessage message, ErgosfareContext context, IServiceProvider serviceProvider)
        => Execute(message, context, serviceProvider);

    /// <inheritdoc />
    public sealed override TReturn Accept<TReturn, TState>(IStagedResultPlanVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage, TResult>(state);

    /// <summary>
    /// Runs the baked pipeline for a dispatch whose group filter is a runtime value: every
    /// participant is present in the body and each call is guarded by the group test the
    /// generator baked for it. Only invoked on a plan that reports
    /// <see cref="StagedResultPlan.FilterGroups"/>; the default forwards to the unfiltered body.
    /// </summary>
    public virtual ValueTask<TResult> ExecuteFiltered(
        TMessage message, ErgosfareContext context, IServiceProvider serviceProvider, IReadOnlyList<string> groups)
        => Execute(message, context, serviceProvider);

    /// <summary>
    /// The direct-construction variant of <see cref="ExecuteFiltered"/>; see
    /// <see cref="ExecuteDirect"/> for when it qualifies.
    /// </summary>
    public virtual ValueTask<TResult> ExecuteFilteredDirect(
        TMessage message, ErgosfareContext context, IServiceProvider serviceProvider, IReadOnlyList<string> groups)
        => ExecuteFiltered(message, context, serviceProvider, groups);

}
