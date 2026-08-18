using System.Collections.Concurrent;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One container's streaming pipelines, keyed by (query type, item type) — the streaming
/// counterpart of <see cref="PipelineExecutorCache"/> and <see cref="FrozenBroadcastTable"/>.
/// </summary>
/// <param name="dependenciesFactory">The factory the pipelines resolve participants through.</param>
/// <remarks>
/// The table belongs to its container rather than the process, so containers need no
/// guarding against each other here.
/// </remarks>
internal sealed class StreamDispatchTable(IMessageDependenciesFactory dependenciesFactory)
{
    private readonly ConcurrentDictionary<(Type QueryType, Type ResultType), object> _byPair = new();

    /// <summary>
    /// Returns the streaming pipeline of a query known by its runtime type.
    /// </summary>
    /// <typeparam name="TResult">The type of the streamed items.</typeparam>
    /// <param name="queryType">The query's runtime type.</param>
    /// <returns>The pipeline for that pair.</returns>
    /// <remarks>
    /// The plan closes the generic without reflection — a compiled stream plan carries its
    /// own query and item types. A pair without one gets the pipeline that fails every
    /// stream, as precisely as the participants allow: nothing is dispatched at run time
    /// that was not produced at compile time.
    /// </remarks>
    internal IStreamDispatch<TResult> Get<TResult>(Type queryType)
    {
        var key = (queryType, typeof(TResult));

        if (_byPair.TryGetValue(key, out var dispatch))
        {
            return (IStreamDispatch<TResult>)dispatch;
        }

        return (IStreamDispatch<TResult>)_byPair.GetOrAdd(
            key,
            GeneratedDispatchRoots.FindStagedStreamPlan(queryType, typeof(TResult)) is { } plan
                ? plan.Accept(PlanDispatchVisitor.Instance, new DispatchState(dependenciesFactory, plan))
                : new UnplannedStreamDispatch<TResult>(dependenciesFactory, queryType));
    }

    /// <summary>
    /// What the visitor needs to construct the executor inside the plan's generic context.
    /// </summary>
    /// <param name="DependenciesFactory">The factory the executor verifies participants through.</param>
    /// <param name="Plan">The plan, held without its type and cast back inside the context.</param>
    private readonly record struct DispatchState(
        IMessageDependenciesFactory DependenciesFactory,
        object Plan);

    /// <summary>
    /// Constructs the executor that hosts a stream plan, inside the generic context the
    /// plan carries.
    /// </summary>
    private sealed class PlanDispatchVisitor : IStagedStreamPlanVisitor<object, DispatchState>
    {
        /// <summary>
        /// The shared instance; the visitor holds no state.
        /// </summary>
        public static readonly PlanDispatchVisitor Instance = new();

        /// <inheritdoc />
        public object Visit<TQuery, TResult>(DispatchState state)
            where TQuery : notnull
            => new FrozenStreamDispatch<TQuery, TResult>(
                state.DependenciesFactory,
                (StagedStreamPlan<TQuery, TResult>)state.Plan);
    }
}
