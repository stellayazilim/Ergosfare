using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Results;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One container's streaming pipeline for a (query, item) pair: it runs the compiled
/// stream plan, and nothing else. A stream the plan cannot serve fails, precisely.
/// </summary>
/// <typeparam name="TQuery">The query type this pipeline serves.</typeparam>
/// <typeparam name="TResult">The type of the streamed items.</typeparam>
/// <remarks>
/// Settled the same way the send pipelines are: the first stream resolves the participants
/// once, verifies the plan against them, and every later stream reads one field and runs
/// the plan. Grouped streams have no compiled plans yet, so a stream that names groups
/// fails as unplanned rather than running a pipeline no plan produced.
/// </remarks>
internal sealed class FrozenStreamDispatch<TQuery, TResult> : IStreamDispatch<TResult>
    where TQuery : notnull
{
    private readonly IMessageDependenciesFactory _factory;
    private readonly StagedStreamPlan<TQuery, TResult> _plan;

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}._verdict"/>
    private volatile int _verdict;

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}.Undecided"/>
    // ReSharper disable once UnusedMember.Local
    private const int Undecided = 0;

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}.UsePlan"/>
    private const int UsePlan = 2;

    /// <summary>
    /// Initializes the pipeline over a container's factory and the plan compiled for this
    /// pair.
    /// </summary>
    /// <param name="dependenciesFactory">The factory participants are verified through.</param>
    /// <param name="plan">The compiled plan for this pair.</param>
    public FrozenStreamDispatch(IMessageDependenciesFactory dependenciesFactory, StagedStreamPlan<TQuery, TResult> plan)
    {
        _factory = dependenciesFactory;
        _plan = plan;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TResult> Stream(
        object query,
        ErgosfareContext? context,
        CancellationToken cancellationToken,
        IServiceProvider serviceProvider,
        IEnumerable<string>? groups)
    {
        if (groups is not (null or List<string> { Count: 0 } or string[] { Length: 0 } or GroupSet { Count: 0 }))
        {
            // No per-set or filtering stream plans are compiled yet; a grouped stream would
            // have to select participants at run time, which is exactly what nothing does
            // any more.
            throw UnplannedDispatch.ForUnplannedGroupSet(typeof(TQuery), [.. groups]);
        }

        if (_verdict != UsePlan)
        {
            Verify(serviceProvider);
        }

        // A context created here is never recycled: enumeration happens after this call
        // returns, so there is no point at which the context is known to be finished with.
        context ??= new ErgosfareContext(cancellationToken: cancellationToken);

        return _plan.Execute((TQuery)query, context, serviceProvider, cancellationToken);
    }

    /// <summary>
    /// Runs the first stream's verification: resolves the participants once, verifies the
    /// plan against them, and settles the verdict — or fails the stream naming what could
    /// not be verified.
    /// </summary>
    /// <param name="serviceProvider">The provider the adapter tier is read from.</param>
    private void Verify(IServiceProvider serviceProvider)
    {
        if (_factory is not MessageDependenciesFactory typedFactory)
        {
            throw UnplannedDispatch.ForForeignFactory(typeof(TQuery));
        }

        // Throws for a query no composition serves — a query nothing handles is a failed
        // dispatch rather than an empty stream — and on every stream of such a query,
        // since nothing is settled until the plan is verified.
        if (typedFactory.Create(typeof(TQuery), []) is not MessageDependencies dependencies)
        {
            throw UnplannedDispatch.ForForeignFactory(typeof(TQuery));
        }

        // Stream plans never assume an adapter over the enumerator, so one being bound
        // means branches the plan does not have.
        if (ResultAdapterBinding.For<TQuery, IAsyncEnumerator<TResult>>(serviceProvider) is { } adapter)
        {
            throw UnplannedDispatch.ForResultAdapterMismatch(
                typeof(TQuery), compiledAdapterType: null, adapter.GetType());
        }

        if (dependencies.ForcedMemoization)
        {
            throw UnplannedDispatch.ForMemoizedInstances(typeof(TQuery));
        }

        if (!StagedPlanGate.Matches(dependencies, _plan.Composition))
        {
            throw UnplannedDispatch.ForDivergedComposition(typeof(TQuery), dependencies, _plan.Composition);
        }

        _verdict = UsePlan;
    }
}
