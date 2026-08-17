using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Results;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One container's result-producing pipeline for one (message, result) pair; the
/// counterpart of <see cref="FrozenVoidDispatch{TMessage}"/>, settling the same way on its
/// first dispatch.
/// </summary>
/// <typeparam name="TMessage">The message type this pipeline serves.</typeparam>
/// <typeparam name="TResult">The result type it produces.</typeparam>
/// <remarks>
/// Having a real result adds one more condition the void side does not have: a plan is only
/// used while the adapter it was compiled against is exactly the one bound here — both
/// absent, in the common case — so a plan compiled without the branches an adapter needs
/// never serves an adapted pipeline.
/// </remarks>
#pragma warning disable CS8714 // TResult is used as a pattern type argument; handler contracts declare notnull results
internal sealed class FrozenResultDispatch<TMessage, TResult> : IPipelineExecutor<TResult>
    where TMessage : IMessage
{

    private readonly IMessageDependenciesFactory _factory;
    private readonly StagedResultPlan<TMessage, TResult>? _plan;
    private readonly GroupedCompositions _grouped;

    // The adapter bound to this pipeline's result type, resolved on the first dispatch as in
    // the void pipeline. Which adapter it is also decides whether a plan may be used.
    private IResultAdapter<TResult>? _resultAdapter;
    private IResultMaterializer<TResult>? _resultMaterializer;
    private volatile bool _resultAdapterResolved;

    private IMessageDependencies? _cachedDependencies;
    private MessageDependencies? _cachedFastDependencies;

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}._verdict"/>
    private volatile int _verdict;

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}.Foreign"/>
    private const int Foreign = -1;

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}.Undecided"/>
    // ReSharper disable once UnusedMember.Local
    private const int Undecided = 0;

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}.UseBody"/>
    private const int UseBody = 1;

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}.UsePlan"/>
    private const int UsePlan = 2;

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}.UsePlanDirect"/>
    private const int UsePlanDirect = 3;

    /// <summary>
    /// Initializes the pipeline over a container's factory and the plan compiled for this
    /// pair, if any.
    /// </summary>
    /// <param name="dependenciesFactory">The factory participants are resolved through.</param>
    /// <param name="plan">The compiled plan for this pair, or <c>null</c>.</param>
    /// <remarks>
    /// Public despite the type being internal, for the same reason its void twin is.
    /// </remarks>
    public FrozenResultDispatch(IMessageDependenciesFactory dependenciesFactory, StagedResultPlan<TMessage, TResult>? plan)
    {
        _factory = dependenciesFactory;
        _plan = plan;
        _grouped = new GroupedCompositions(dependenciesFactory, typeof(TMessage), AdmitGroupedPlan);

        if (dependenciesFactory is not MessageDependenciesFactory)
        {
            _verdict = Foreign;
        }
    }

    /// <inheritdoc />
    public ValueTask<TResult> Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups)
    {
        if (groups is not null)
        {
            return ExecuteGrouped(message, context, serviceProvider, groups);
        }

        var verdict = _verdict;

        if (verdict == UseBody)
        {
            return ExecuteRuntimeLane(message, _cachedDependencies!, _cachedFastDependencies, context, serviceProvider);
        }

        if (verdict >= UsePlan)
        {
            return verdict == UsePlanDirect
                ? _plan!.ExecuteDirect((TMessage)message, context, serviceProvider)
                : _plan!.Execute((TMessage)message, context, serviceProvider);
        }

        if (verdict == Foreign)
        {
            EnsureResultAdapter(serviceProvider);
            return ExecuteRuntimeLane(
                message, _factory.Create(typeof(TMessage), []), fast: null, context, serviceProvider);
        }

        return ExecuteUndecided(message, context, serviceProvider);
    }

    /// <summary>
    /// Runs the first dispatch and settles which route the rest take; see
    /// <see cref="FrozenVoidDispatch{TMessage}.ExecuteUndecided"/>.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <returns>The result the pipeline produced.</returns>
    private ValueTask<TResult> ExecuteUndecided(object message, ErgosfareContext context, IServiceProvider serviceProvider)
    {
        EnsureResultAdapter(serviceProvider);

        var typedFactory = (MessageDependenciesFactory)_factory;

        var dependencies = typedFactory.Create(typeof(TMessage), []);
        var fastDependencies = dependencies as MessageDependencies;
        _cachedFastDependencies = fastDependencies;
        _cachedDependencies = dependencies;

        var usePlan = _plan is not null
            && _plan.Composition.ResultAdapterType == _resultAdapter?.GetType()
            && fastDependencies is { MemoizedInstances: false }
            && StagedPlanGate.Matches(fastDependencies, _plan.Composition);

        _verdict = usePlan
            ? _plan!.SupportsDirectConstruction
              && StagedPlanGate.AllPlainTransient(typedFactory, _plan.Composition)
                ? UsePlanDirect
                : UsePlan
            : UseBody;

        if (_verdict >= UsePlan)
        {
            return _verdict == UsePlanDirect
                ? _plan!.ExecuteDirect((TMessage)message, context, serviceProvider)
                : _plan!.Execute((TMessage)message, context, serviceProvider);
        }

        return ExecuteRuntimeLane(message, dependencies, fastDependencies, context, serviceProvider);
    }

    /// <summary>
    /// Runs the resolved participants; see
    /// <see cref="FrozenVoidDispatch{TMessage}.ExecuteRuntimeLane"/>.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="dependencies">The participants to run.</param>
    /// <param name="fast">
    /// The same participants as their concrete type, when they are one; <c>null</c> rules
    /// out the single-handler route.
    /// </param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <returns>The result the pipeline produced.</returns>
    private ValueTask<TResult> ExecuteRuntimeLane(
        object message, IMessageDependencies dependencies, MessageDependencies? fast,
        ErgosfareContext context, IServiceProvider serviceProvider)
    {
        if (fast?.FastSingleHandler is { } handlerReference && _resultAdapter is null)
        {
            var handler = handlerReference.Resolve(serviceProvider);

            switch (handler)
            {
                case IAsyncHandler<TMessage, TResult> asyncHandler:
                    return asyncHandler.HandleAsync((TMessage)message, context);
                case IHandler<TMessage, ValueTask<TResult>> valueTaskShaped:
                    return valueTaskShaped.Handle((TMessage)message, context);
                case IHandler<TMessage, TResult> syncHandler:
                    return ValueTask.FromResult(syncHandler.Handle((TMessage)message, context));
            }

            // The handler implements no contract this route can call; falling through lets
            // the body raise the one exception that says so.
        }

        return ResultPipelineBody<TMessage, TResult>.Run(
            (TMessage)message, dependencies, _resultAdapter, _resultMaterializer, context, serviceProvider);
    }

    /// <summary>
    /// Runs a dispatch that named groups; see
    /// <see cref="FrozenVoidDispatch{TMessage}.ExecuteGrouped"/>.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <param name="groups">The groups the dispatch asked for.</param>
    /// <returns>The result the pipeline produced.</returns>
    private ValueTask<TResult> ExecuteGrouped(object message, ErgosfareContext context,
        IServiceProvider serviceProvider, IEnumerable<string> groups)
    {
        EnsureResultAdapter(serviceProvider);

        var composition = _grouped.Resolve(groups);

        if (composition.Admission.Plan is StagedResultPlan<TMessage, TResult> groupedPlan)
        {
            // A plan compiled for this exact set already knows its participants; the
            // filtering plan works them out from the set it is handed.
            if (groupedPlan.FilterGroups is not null)
            {
                return composition.Admission.Direct
                    ? groupedPlan.ExecuteFilteredDirect((TMessage)message, context, serviceProvider, composition.Groups)
                    : groupedPlan.ExecuteFiltered((TMessage)message, context, serviceProvider, composition.Groups);
            }

            return composition.Admission.Direct
                ? groupedPlan.ExecuteDirect((TMessage)message, context, serviceProvider)
                : groupedPlan.Execute((TMessage)message, context, serviceProvider);
        }

        return ExecuteRuntimeLane(message, composition.Dependencies, composition.Fast, context, serviceProvider);
    }

    /// <summary>
    /// Decides which compiled plan, if any, may serve one group set; see
    /// <see cref="FrozenVoidDispatch{TMessage}.AdmitGroupedPlan"/>.
    /// </summary>
    /// <param name="groups">The group set being decided for.</param>
    /// <param name="dependencies">The participants that set selects.</param>
    /// <returns>The plan and whether it may construct participants itself.</returns>
    /// <remarks>
    /// The adapter must match here too, exactly as it must for the ungrouped route.
    /// </remarks>
    private GroupedPlanAdmission AdmitGroupedPlan(string[] groups, IMessageDependencies dependencies)
    {
        var plan = GeneratedDispatchRoots.FindStagedResultPlan(typeof(TMessage), typeof(TResult), groups)
            as StagedResultPlan<TMessage, TResult>;

        if (plan is not null)
        {
            if (plan.Composition.ResultAdapterType != _resultAdapter?.GetType()
                || dependencies is not MessageDependencies { MemoizedInstances: false } keyed
                || !StagedPlanGate.Matches(keyed, plan.Composition))
            {
                return default;
            }
        }
        else
        {
            // No plan was compiled for this set; the void dispatch explains what the
            // filtering plan has to be checked against.
            plan = GeneratedDispatchRoots.FindFilteredResultPlan(typeof(TMessage), typeof(TResult))
                as StagedResultPlan<TMessage, TResult>;

            if (plan?.FilterGroups is not { } covered
                || plan.Composition.ResultAdapterType != _resultAdapter?.GetType()
                || _factory.Find(typeof(TMessage), covered) is not MessageDependencies { MemoizedInstances: false } full
                || !StagedPlanGate.Matches(full, plan.Composition))
            {
                return default;
            }
        }

        var direct = plan.SupportsDirectConstruction
                     && _factory is MessageDependenciesFactory typedFactory
                     && StagedPlanGate.AllPlainTransient(typedFactory, plan.Composition);

        return new GroupedPlanAdmission(plan, direct);
    }

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}.EnsureResultAdapter"/>
    private void EnsureResultAdapter(IServiceProvider serviceProvider)
    {
        if (_resultAdapterResolved)
        {
            return;
        }

        var adapter = ResultAdapterBinding.For<TMessage, TResult>(serviceProvider);
        _resultAdapter = adapter;
        _resultMaterializer = adapter as IResultMaterializer<TResult>;
        _resultAdapterResolved = true;
    }
}
#pragma warning restore CS8714
