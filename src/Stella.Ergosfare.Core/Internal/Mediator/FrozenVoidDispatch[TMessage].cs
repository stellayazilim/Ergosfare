using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Results;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One container's void pipeline for one message type: it runs the compiled plan when this
/// container's participants are the ones the plan was compiled against, and the general
/// body otherwise.
/// </summary>
/// <typeparam name="TMessage">The message type this pipeline serves.</typeparam>
/// <remarks>
/// Both are the same pipeline reached two ways, so one type covers them. Everything is
/// settled on the first dispatch — the participants are resolved, the adapter is bound, and
/// the plan is accepted or rejected against them — after which every dispatch reads one
/// field and runs what it names. A group-filtered dispatch picks its own participants
/// through a per-set slot and runs the same body.
/// </remarks>
internal sealed class FrozenVoidDispatch<TMessage> : IPipelineExecutor
    where TMessage : IMessage
{

    private readonly IMessageDependenciesFactory _factory;
    private readonly StagedVoidPlan<TMessage>? _plan;
    private readonly GroupedCompositions _grouped;

    // The adapter bound to this pipeline's Unit slot, together with its materializer facet.
    // Resolved on the first dispatch because the container's default tier needs a provider,
    // and null unless a Unit adapter was deliberately bound.
    private IResultAdapter<Unit>? _resultAdapter;
    private IResultMaterializer<Unit>? _resultMaterializer;
    private volatile bool _resultAdapterResolved;

    private IMessageDependencies? _cachedDependencies;
    private MessageDependencies? _cachedFastDependencies;

    /// <summary>
    /// Which route an ungrouped dispatch takes, decided on the first one and never
    /// revisited.
    /// </summary>
    /// <remarks>
    /// Volatile because writing it publishes everything decided alongside it: a reader that
    /// sees a settled verdict must also see the cached participants and the resolved
    /// adapter, which are written first.
    /// </remarks>
    private volatile int _verdict;

    /// <summary>
    /// The factory came from outside, so nothing is settled and every dispatch asks it
    /// again.
    /// </summary>
    private const int Foreign = -1;

    /// <summary>
    /// Nothing has been decided yet — the value a new instance starts at, which is why it is
    /// never compared against.
    /// </summary>
    // ReSharper disable once UnusedMember.Local
    private const int Undecided = 0;

    /// <summary>
    /// Run the general body over the resolved participants.
    /// </summary>
    private const int UseBody = 1;

    /// <summary>
    /// Run the compiled plan.
    /// </summary>
    private const int UsePlan = 2;

    /// <summary>
    /// Run the compiled plan and let it construct participants itself.
    /// </summary>
    private const int UsePlanDirect = 3;

    /// <summary>
    /// Initializes the pipeline over a container's factory and the plan compiled for this
    /// message, if any.
    /// </summary>
    /// <param name="dependenciesFactory">The factory participants are resolved through.</param>
    /// <param name="plan">The compiled plan for this message, or <c>null</c>.</param>
    /// <remarks>
    /// Public despite the type being internal: the reflective fallback for message types
    /// without a generated root constructs through <c>Activator</c>, which only binds public
    /// constructors.
    /// </remarks>
    public FrozenVoidDispatch(IMessageDependenciesFactory dependenciesFactory, StagedVoidPlan<TMessage>? plan)
    {
        _factory = dependenciesFactory;
        _plan = plan;
        _grouped = new GroupedCompositions(dependenciesFactory, typeof(TMessage), AdmitGroupedPlan);

        if (dependenciesFactory is not MessageDependenciesFactory)
        {
            // A factory from outside promises nothing about answering the same twice, so
            // nothing is settled: every dispatch asks it and runs the body.
            _verdict = Foreign;
        }
    }

    /// <inheritdoc />
    public ValueTask Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider,
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
    /// Runs the first dispatch: binds the adapter, resolves the participants, decides
    /// whether the plan may be used, then takes the route it settled on.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    private ValueTask ExecuteUndecided(object message, ErgosfareContext context, IServiceProvider serviceProvider)
    {
        EnsureResultAdapter(serviceProvider);

        var typedFactory = (MessageDependenciesFactory)_factory;

        // Throws for a message no composition serves — and on every dispatch of such a
        // message, since nothing is cached when this throws.
        var dependencies = typedFactory.Create(typeof(TMessage), []);
        var fastDependencies = dependencies as MessageDependencies;
        _cachedFastDependencies = fastDependencies;
        _cachedDependencies = dependencies;

        // Void plans never assume an adapter, so any adapter bound here keeps the dispatch
        // on the general body, which knows how to consult one.
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
    /// Runs the resolved participants: the handler on its own when the pipeline is a single
    /// handler with no interceptors, and the general body otherwise.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="dependencies">The participants to run.</param>
    /// <param name="fast">
    /// The same participants as their concrete type, when they are one; <c>null</c> rules
    /// out the single-handler route.
    /// </param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    /// <remarks>
    /// There is no abort handling here on purpose: the engine's own frame owns that, and an
    /// exception-handling region in this method would stop it being inlined into its caller
    /// on every dispatch.
    /// </remarks>
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

            // The handler implements no contract this route can call; falling through lets
            // the body raise the one exception that says so.
        }

        return VoidPipelineBody<TMessage>.Run(
            (TMessage)message, dependencies, _resultAdapter, _resultMaterializer, context, serviceProvider);
    }

    /// <summary>
    /// Runs a dispatch that named groups, over the participants those groups select.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <param name="groups">The groups the dispatch asked for.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    private ValueTask ExecuteGrouped(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string> groups)
    {
        EnsureResultAdapter(serviceProvider);

        var composition = _grouped.Resolve(groups);

        if (composition.Admission.Plan is StagedVoidPlan<TMessage> groupedPlan)
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
    /// Decides which compiled plan, if any, may serve one group set.
    /// </summary>
    /// <param name="groups">The group set being decided for.</param>
    /// <param name="dependencies">The participants that set selects.</param>
    /// <returns>The plan and whether it may construct participants itself.</returns>
    /// <remarks>
    /// The same question <see cref="ExecuteUndecided"/> answers for the default set, asked
    /// once per set as its entry is built rather than on every dispatch.
    /// </remarks>
    private GroupedPlanAdmission AdmitGroupedPlan(string[] groups, IMessageDependencies dependencies)
    {
        if (_resultAdapter is not null)
        {
            return default;
        }

        var plan = GeneratedDispatchRoots.FindStagedVoidPlan(typeof(TMessage), groups) as StagedVoidPlan<TMessage>;

        if (plan is not null)
        {
            if (dependencies is not MessageDependencies { MemoizedInstances: false } keyed
                || !StagedPlanGate.Matches(keyed, plan.Composition))
            {
                return default;
            }
        }
        else
        {
            // No plan was compiled for this set. The filtering plan can serve any set, but
            // only if it is checked against the participants of the groups it covers —
            // the one set that reproduces everything its body holds.
            plan = GeneratedDispatchRoots.FindFilteredVoidPlan(typeof(TMessage)) as StagedVoidPlan<TMessage>;

            if (plan?.FilterGroups is not { } covered
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

    /// <summary>
    /// Binds this pipeline's result adapter, once.
    /// </summary>
    /// <param name="serviceProvider">The provider the container's default adapter comes from.</param>
    /// <remarks>
    /// The container is sealed once built, so the binding can never change afterwards. Two
    /// threads racing here both publish the same thing, and the volatile flag orders the
    /// publication.
    /// </remarks>
    private void EnsureResultAdapter(IServiceProvider serviceProvider)
    {
        if (_resultAdapterResolved)
        {
            return;
        }

        var adapter = ResultAdapterBinding.For<TMessage, Unit>(serviceProvider);
        _resultAdapter = adapter;
        // Whether an adapter can also build a failed result is up to whoever wrote it; none
        // in this repository does, which is what the test is for.
        // ReSharper disable once SuspiciousTypeConversion.Global
        _resultMaterializer = adapter as IResultMaterializer<Unit>;
        _resultAdapterResolved = true;
    }
}
