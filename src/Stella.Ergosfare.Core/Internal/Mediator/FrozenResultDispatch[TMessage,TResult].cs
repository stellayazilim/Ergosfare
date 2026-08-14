using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Results;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Result-producing counterpart of <see cref="FrozenVoidDispatch{TMessage}"/>: one type for
/// the compiled staged plan and the runtime body, decided once with the composition.
/// </summary>
/// <remarks>
/// The result slot adds one gate input the void side does not have: the plan is only
/// trusted while its baked adapter is exactly the one the runtime binds (both null in the
/// overwhelmingly common case), so a plan emitted without the slot's value-path branches
/// never serves an adapted pipeline.
/// </remarks>
#pragma warning disable CS8714 // TResult is used as a pattern type argument; handler contracts declare notnull results
internal sealed class FrozenResultDispatch<TMessage, TResult> : IPipelineExecutor<TResult>
    where TMessage : IMessage
{
    private static readonly string[] EmptyGroups = [];

    private readonly IMessageDependenciesFactory _factory;
    private readonly StagedResultPlan<TMessage, TResult>? _plan;
    private readonly GroupedCompositions _grouped;

    // The effective adapter of this pipeline's closed result slot, resolved once on the
    // first dispatch; see FrozenVoidDispatch. The identity also feeds the plan gate.
    private IResultAdapter<TResult>? _resultAdapter;
    private IResultMaterializer<TResult>? _resultMaterializer;
    private volatile bool _resultAdapterResolved;

    private IMessageDependencies? _cachedDependencies;
    private MessageDependencies? _cachedFastDependencies;

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}._verdict"/>
    private volatile int _verdict;

    private const int Foreign = -1;
    private const int Undecided = 0;
    private const int UseBody = 1;
    private const int UsePlan = 2;
    private const int UsePlanDirect = 3;

    // Public within the internal type: the reflective fallback for unrooted runtime types
    // constructs through Activator, which only binds public constructors.
    public FrozenResultDispatch(IMessageDependenciesFactory dependenciesFactory, StagedResultPlan<TMessage, TResult>? plan)
    {
        _factory = dependenciesFactory;
        _plan = plan;
        _grouped = new GroupedCompositions(dependenciesFactory, typeof(TMessage));

        if (dependenciesFactory is not MessageDependenciesFactory)
        {
            _verdict = Foreign;
        }
    }

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
                message, _factory.Create(typeof(TMessage), EmptyGroups), fast: null, context, serviceProvider);
        }

        return ExecuteUndecided(message, context, serviceProvider);
    }

    /// <summary>
    /// The first dispatch; see <see cref="FrozenVoidDispatch{TMessage}.ExecuteUndecided"/>.
    /// </summary>
    private ValueTask<TResult> ExecuteUndecided(object message, ErgosfareContext context, IServiceProvider serviceProvider)
    {
        EnsureResultAdapter(serviceProvider);

        var typedFactory = (MessageDependenciesFactory)_factory;

        var dependencies = typedFactory.Create(typeof(TMessage), EmptyGroups);
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
    /// The frozen composition's delivery; see
    /// <see cref="FrozenVoidDispatch{TMessage}.ExecuteRuntimeLane"/>.
    /// </summary>
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

            // Unsupported handler contract: fall through so the body raises its canonical
            // NotSupportedException.
        }

        return ResultPipelineBody<TMessage, TResult>.Run(
            (TMessage)message, dependencies, _resultAdapter, _resultMaterializer, context, serviceProvider);
    }

    /// <summary>
    /// The group-filtered dispatch; see
    /// <see cref="FrozenVoidDispatch{TMessage}.ExecuteGrouped"/>.
    /// </summary>
    private ValueTask<TResult> ExecuteGrouped(object message, ErgosfareContext context,
        IServiceProvider serviceProvider, IEnumerable<string> groups)
    {
        EnsureResultAdapter(serviceProvider);

        var composition = _grouped.Resolve(groups);

        return ExecuteRuntimeLane(message, composition.Dependencies, composition.Fast, context, serviceProvider);
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
