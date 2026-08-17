
namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>
/// <see cref="StagedVoidPlan"/> closed over its message type; generated plans derive from
/// this, and hand-written ones may too.
/// </summary>
/// <typeparam name="TMessage">The message this plan serves.</typeparam>
public abstract class StagedVoidPlan<TMessage> : StagedVoidPlan
    where TMessage : IMessage
{
    /// <summary>
    /// Runs the compiled pipeline for <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">
    /// The provider participants are resolved from — the dispatching scope's.
    /// </param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    /// <remarks>
    /// Only called while the live pipeline still matches
    /// <see cref="StagedVoidPlan.Composition"/>.
    /// </remarks>
    public abstract ValueTask Execute(TMessage message, ErgosfareContext context, IServiceProvider serviceProvider);

    /// <summary>
    /// Runs the compiled pipeline with participants constructed directly rather than
    /// resolved.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">
    /// The provider participants' own dependencies are resolved from.
    /// </param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    /// <remarks>
    /// Only called when <see cref="StagedVoidPlan.SupportsDirectConstruction"/> is
    /// <c>true</c> and the executor has confirmed every participant's plain transient
    /// registration. The default implementation runs <see cref="Execute"/> instead.
    /// </remarks>
    public virtual ValueTask ExecuteDirect(TMessage message, ErgosfareContext context, IServiceProvider serviceProvider)
        => Execute(message, context, serviceProvider);

    /// <summary>
    /// Calls <paramref name="visitor"/> with <typeparamref name="TMessage"/> as its generic
    /// argument.
    /// </summary>
    /// <typeparam name="TReturn">What the visitor produces.</typeparam>
    /// <typeparam name="TState">The state the visitor needs.</typeparam>
    /// <param name="visitor">The visitor to call.</param>
    /// <param name="state">Passed to the visitor unchanged.</param>
    /// <returns>Whatever the visitor produced.</returns>
    public sealed override TReturn Accept<TReturn, TState>(IStagedVoidPlanVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage>(state);

    /// <summary>
    /// Runs the compiled pipeline for a dispatch whose groups are only known now, testing
    /// each participant's groups before calling it.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <param name="groups">The groups the dispatch asked for.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    /// <remarks>
    /// Only called on a plan that reports <see cref="StagedVoidPlan.FilterGroups"/>. The
    /// default implementation runs the unfiltered body.
    /// </remarks>
    public virtual ValueTask ExecuteFiltered(
        TMessage message, ErgosfareContext context, IServiceProvider serviceProvider, IReadOnlyList<string> groups)
        => Execute(message, context, serviceProvider);

    /// <summary>
    /// Runs <see cref="ExecuteFiltered"/> with participants constructed directly; see
    /// <see cref="ExecuteDirect"/> for when that applies.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">
    /// The provider participants' own dependencies are resolved from.
    /// </param>
    /// <param name="groups">The groups the dispatch asked for.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    public virtual ValueTask ExecuteFilteredDirect(
        TMessage message, ErgosfareContext context, IServiceProvider serviceProvider, IReadOnlyList<string> groups)
        => ExecuteFiltered(message, context, serviceProvider, groups);

}
