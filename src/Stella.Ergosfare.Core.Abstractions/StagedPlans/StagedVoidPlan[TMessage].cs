using Stella.Ergosfare.Core.Abstractions.Results;

namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>
/// <see cref="StagedVoidPlan"/> closed over its message type; generated plans derive from
/// this, and hand-written ones may too.
/// </summary>
/// <typeparam name="TMessage">The message this plan serves.</typeparam>
public abstract class StagedVoidPlan<TMessage> : StagedVoidPlan, IPipelineExecutor, ICompiledPlan
    where TMessage : IMessage
{
    ValueTask IPipelineExecutor.Execute(object message, ErgosfareContext context,
        IServiceProvider serviceProvider, IEnumerable<string>? groups)
        => FilterGroups is not null && groups is IReadOnlyList<string> requested
            ? ExecuteFiltered((TMessage)message, context, serviceProvider, requested)
            : Execute((TMessage)message, context, serviceProvider);

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

}
