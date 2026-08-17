

namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;
/// <summary>
/// <see cref="StagedResultPlan"/> closed over its message and result types; generated
/// plans derive from this, and hand-written ones may too.
/// </summary>
/// <typeparam name="TMessage">The message this plan serves.</typeparam>
/// <typeparam name="TResult">The result this plan produces.</typeparam>
public abstract class StagedResultPlan<TMessage, TResult> : StagedResultPlan
    where TMessage : IMessage
{
    /// <summary>
    /// Runs the compiled pipeline for <paramref name="message"/> and returns its result.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">
    /// The provider participants are resolved from — the dispatching scope's.
    /// </param>
    /// <returns>The result the pipeline produced.</returns>
    /// <remarks>
    /// Only called while the live pipeline still matches
    /// <see cref="StagedResultPlan.Composition"/>.
    /// </remarks>
    public abstract ValueTask<TResult> Execute(TMessage message, ErgosfareContext context, IServiceProvider serviceProvider);

    /// <summary>
    /// Runs the compiled pipeline with participants constructed directly rather than
    /// resolved; see <see cref="StagedVoidPlan{TMessage}.ExecuteDirect"/> for when that
    /// applies.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">
    /// The provider participants' own dependencies are resolved from.
    /// </param>
    /// <returns>The result the pipeline produced.</returns>
    public virtual ValueTask<TResult> ExecuteDirect(TMessage message, ErgosfareContext context, IServiceProvider serviceProvider)
        => Execute(message, context, serviceProvider);

    /// <summary>
    /// Calls <paramref name="visitor"/> with <typeparamref name="TMessage"/> and
    /// <typeparamref name="TResult"/> as its generic arguments.
    /// </summary>
    /// <typeparam name="TReturn">What the visitor produces.</typeparam>
    /// <typeparam name="TState">The state the visitor needs.</typeparam>
    /// <param name="visitor">The visitor to call.</param>
    /// <param name="state">Passed to the visitor unchanged.</param>
    /// <returns>Whatever the visitor produced.</returns>
    public sealed override TReturn Accept<TReturn, TState>(IStagedResultPlanVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage, TResult>(state);

    /// <summary>
    /// Runs the compiled pipeline for a dispatch whose groups are only known now, testing
    /// each participant's groups before calling it.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <param name="groups">The groups the dispatch asked for.</param>
    /// <returns>The result the pipeline produced.</returns>
    /// <remarks>
    /// Only called on a plan that reports <see cref="StagedResultPlan.FilterGroups"/>. The
    /// default implementation runs the unfiltered body.
    /// </remarks>
    public virtual ValueTask<TResult> ExecuteFiltered(
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
    /// <returns>The result the pipeline produced.</returns>
    public virtual ValueTask<TResult> ExecuteFilteredDirect(
        TMessage message, ErgosfareContext context, IServiceProvider serviceProvider, IReadOnlyList<string> groups)
        => ExecuteFiltered(message, context, serviceProvider, groups);

}
