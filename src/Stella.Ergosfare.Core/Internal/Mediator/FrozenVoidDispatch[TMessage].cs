using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Results;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One container's frozen void pipeline for one message type: the compiled staged plan when
/// the container's composition is the one it was baked against, the runtime body otherwise —
/// one type for both, because they are the same pipeline filled two ways, not two mechanism
/// families.
/// </summary>
/// <remarks>
/// Everything is decided once. The composition resolves on the first dispatch (the adapter
/// tier needs a provider, which construction does not have), the plan is admitted or refused
/// against it there, and from then on every dispatch reads one field and executes what it
/// names: the plan, or the frozen composition through
/// <see cref="VoidPipelineBody{TMessage}"/>. No gate, no strategy instance, and no
/// dependency materialization ever runs again — a group-filtered dispatch selects its own
/// frozen composition through the per-set slot and runs the same body.
/// </remarks>
internal sealed class FrozenVoidDispatch<TMessage> : IPipelineExecutor
    where TMessage : IMessage
{
    private static readonly string[] EmptyGroups = [];

    private readonly IMessageDependenciesFactory _factory;
    private readonly StagedVoidPlan<TMessage>? _plan;
    private readonly GroupedCompositions _grouped;

    // The effective adapter of the void pipeline's Unit slot — the attribute tiers plus
    // the container's default, resolved once on the first dispatch (the default tier
    // needs the provider) and published through the volatile flag; null unless a Unit
    // adapter is deliberately bound.
    private IResultAdapter<Unit>? _resultAdapter;
    private IResultMaterializer<Unit>? _resultMaterializer;
    private volatile bool _resultAdapterResolved;

    private IMessageDependencies? _cachedDependencies;
    private MessageDependencies? _cachedFastDependencies;

    /// <summary>
    /// The frozen decision: which arm every group-less dispatch takes, decided with the
    /// composition on the first dispatch and never revisited.
    /// </summary>
    /// <remarks>
    /// Volatile because the decision publishes its companions: a reader that observes
    /// <see cref="UseBody"/> must also observe the cached composition and the resolved
    /// adapter fields written before it. The acquiring read costs what the old lane's
    /// per-dispatch volatile adapter flag cost — nothing was added to the hot path.
    /// </remarks>
    private volatile int _verdict;

    private const int Foreign = -1;
    private const int Undecided = 0;
    private const int UseBody = 1;
    private const int UsePlan = 2;
    private const int UsePlanDirect = 3;

    // Public within the internal type: the reflective fallback for unrooted runtime types
    // constructs through Activator, which only binds public constructors.
    public FrozenVoidDispatch(IMessageDependenciesFactory dependenciesFactory, StagedVoidPlan<TMessage>? plan)
    {
        _factory = dependenciesFactory;
        _plan = plan;
        _grouped = new GroupedCompositions(dependenciesFactory, typeof(TMessage), AdmitGroupedPlan);

        if (dependenciesFactory is not MessageDependenciesFactory)
        {
            // A foreign factory promises nothing about answering the same twice, so nothing
            // is frozen: every dispatch asks it and runs the body — the original contract.
            _verdict = Foreign;
        }
    }

    public ValueTask Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups)
    {
        if (groups is not null)
        {
            return ExecuteGrouped(message, context, serviceProvider, groups);
        }

        var verdict = _verdict;

        // The runtime lane's steady state: the frozen composition, through the fast
        // single-handler seam when it applies and the full body when it does not.
        if (verdict == UseBody)
        {
            return ExecuteRuntimeLane(message, _cachedDependencies!, _cachedFastDependencies, context, serviceProvider);
        }

        // The planned lane's steady state: one field read, then the plan.
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
                message, _factory.Create(typeof(TMessage), EmptyGroups), fast: null, context, serviceProvider);
        }

        return ExecuteUndecided(message, context, serviceProvider);
    }

    /// <summary>
    /// The first dispatch: resolve the adapter slot and the composition, admit or refuse
    /// the plan against them — once — then take whichever arm was decided.
    /// </summary>
    private ValueTask ExecuteUndecided(object message, ErgosfareContext context, IServiceProvider serviceProvider)
    {
        EnsureResultAdapter(serviceProvider);

        var typedFactory = (MessageDependenciesFactory)_factory;

        // Throws NoHandlerFoundException for a message no composition serves — every
        // dispatch of such a message, since nothing is cached on the throw.
        var dependencies = typedFactory.Create(typeof(TMessage), EmptyGroups);
        var fastDependencies = dependencies as MessageDependencies;
        _cachedFastDependencies = fastDependencies;
        _cachedDependencies = dependencies;

        // Void plans never model an adapter (a Unit carrier is a deliberate oddity), so
        // any bound adapter keeps the dispatch on the runtime body, which probes it.
        var usePlan = _plan is not null
            && _resultAdapter is null
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
    /// The frozen composition's delivery: the zero-interceptor single-handler seam invokes
    /// the handler's typed member directly — no async state machine, no interface-dispatched
    /// Count checks — and everything else runs the full body. The abort arm lives in the
    /// engine's dispatch frame; an exception-handling region here would keep this method out
    /// of its caller on every dispatch.
    /// </summary>
    private ValueTask ExecuteRuntimeLane(
        object message, IMessageDependencies dependencies, MessageDependencies? fast,
        ErgosfareContext context, IServiceProvider serviceProvider)
    {
        if (fast?.FastSingleHandler is { } handlerReference && _resultAdapter is null)
        {
            var handler = handlerReference.Resolve(serviceProvider);

            switch (handler)
            {
                case IAsyncHandler<TMessage> asyncHandler:
                    return asyncHandler.HandleAsync((TMessage)message, context);
                case IHandler<TMessage, ValueTask> valueTaskShaped:
                    return valueTaskShaped.Handle((TMessage)message, context);
                case IHandler<TMessage, object> syncHandler:
                    syncHandler.Handle((TMessage)message, context);
                    return ValueTask.CompletedTask;
            }

            // Unsupported handler contract: fall through so the body raises its canonical
            // NotSupportedException.
        }

        return VoidPipelineBody<TMessage>.Run(
            (TMessage)message, dependencies, _resultAdapter, _resultMaterializer, context, serviceProvider);
    }

    /// <summary>
    /// The group-filtered dispatch: the composition the filter selects — one frozen
    /// composition per set, through the per-set slot — delivered by the same runtime lane.
    /// A plan is baked against the unfiltered composition, so a filter that could exclude
    /// a planned participant never takes it.
    /// </summary>
    private ValueTask ExecuteGrouped(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string> groups)
    {
        EnsureResultAdapter(serviceProvider);

        var composition = _grouped.Resolve(groups);

        if (composition.Admission.Plan is StagedVoidPlan<TMessage> groupedPlan)
        {
            return composition.Admission.Direct
                ? groupedPlan.ExecuteDirect((TMessage)message, context, serviceProvider)
                : groupedPlan.Execute((TMessage)message, context, serviceProvider);
        }

        return ExecuteRuntimeLane(message, composition.Dependencies, composition.Fast, context, serviceProvider);
    }

    /// <summary>
    /// The compiled plan for one group set, admitted against that set's own composition —
    /// the same question <see cref="ExecuteUndecided"/> answers for the default set, asked
    /// once per set when its entry is built rather than per dispatch.
    /// </summary>
    private GroupedPlanAdmission AdmitGroupedPlan(string[] groups, IMessageDependencies dependencies)
    {
        if (_resultAdapter is not null
            || GeneratedDispatchRoots.FindStagedVoidPlan(typeof(TMessage), groups) is not StagedVoidPlan<TMessage> plan
            || dependencies is not MessageDependencies { MemoizedInstances: false } fast
            || !StagedPlanGate.Matches(fast, plan.Composition))
        {
            return default;
        }

        var direct = plan.SupportsDirectConstruction
                     && _factory is MessageDependenciesFactory typedFactory
                     && StagedPlanGate.AllPlainTransient(typedFactory, plan.Composition);

        return new GroupedPlanAdmission(plan, direct);
    }

    /// <summary>
    /// Resolves the slot's effective adapter once. The container is sealed after build, so
    /// the resolution can never change; a duplicate-resolution race is benign — both
    /// writers publish equivalent state, and the volatile flag orders the publication.
    /// </summary>
    private void EnsureResultAdapter(IServiceProvider serviceProvider)
    {
        if (_resultAdapterResolved)
        {
            return;
        }

        var adapter = ResultAdapterBinding.For<TMessage, Unit>(serviceProvider);
        _resultAdapter = adapter;
        _resultMaterializer = adapter as IResultMaterializer<Unit>;
        _resultAdapterResolved = true;
    }
}
