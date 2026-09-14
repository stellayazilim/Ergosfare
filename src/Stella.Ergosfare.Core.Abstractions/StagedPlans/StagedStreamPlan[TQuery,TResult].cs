using Stella.Ergosfare.Core.Abstractions.Results;

namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>
/// <see cref="StagedStreamPlan"/> closed over its query and item types; generated plans
/// derive from this.
/// </summary>
/// <typeparam name="TQuery">The streaming query this plan serves.</typeparam>
/// <typeparam name="TResult">The type of the items it streams.</typeparam>
public abstract class StagedStreamPlan<TQuery, TResult> : StagedStreamPlan, ICompiledStreamPlan<TResult>
    where TQuery : notnull
{
    IAsyncEnumerable<TResult> ICompiledStreamPlan<TResult>.Execute(object message,
        ErgosfareContext context, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => Execute((TQuery)message, context, serviceProvider, cancellationToken);

    /// <summary>
    /// Streams the results of <paramref name="query"/> through the compiled pipeline.
    /// </summary>
    /// <param name="query">The query to run.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">
    /// The provider participants are resolved from — the dispatching scope's.
    /// </param>
    /// <param name="cancellationToken">The token the stream is enumerated under.</param>
    /// <returns>
    /// The streamed items. Nothing runs until the caller begins enumerating, and the
    /// stages after the handler run once enumeration ends.
    /// </returns>
    /// <remarks>
    /// Only called while the live pipeline still matches
    /// <see cref="StagedStreamPlan.Composition"/>.
    /// </remarks>
    public abstract IAsyncEnumerable<TResult> Execute(
        TQuery query, ErgosfareContext context, IServiceProvider serviceProvider,
        CancellationToken cancellationToken);

}
