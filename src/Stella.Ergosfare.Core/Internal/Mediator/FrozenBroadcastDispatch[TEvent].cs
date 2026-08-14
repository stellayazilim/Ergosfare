using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>The typed closure of <see cref="FrozenBroadcastDispatch"/>.</summary>
/// <remarks>
/// <para>
/// Everything is decided in the constructor: the group-less composition is resolved once,
/// the compiled plan is admitted or refused against it once, and the outcome is a readonly
/// mode — there is no verdict field a dispatch writes, no gate a dispatch consults, and no
/// dependency materialization on any publish. The composition is settled before the
/// container is built, so deciding at construction observes exactly what the first publish
/// would have.
/// </para>
/// <para>
/// The delivery bodies below are the plan family's N-handler base case, hosted in the same
/// closed type that owns the decision: the bare loop for the interceptorless composition,
/// the staged pipeline for the interceptor-bearing one. A single-handler pipeline is the
/// N = 1 case of these bodies — there is no separate strategy family to fall into.
/// </para>
/// </remarks>
internal sealed class FrozenBroadcastDispatch<TEvent> : FrozenBroadcastDispatch
    where TEvent : notnull
{
    // One copy per closed message type is deliberate — the dispatch itself is per type.
    // ReSharper disable once StaticMemberInGenericType
    private static readonly string[] EmptyGroups = [];

    /// <summary>
    /// The compiled plan for this message type. Null when the generator modeled none — an
    /// unintercepted broadcast, or one whose handler set it could not model exactly.
    /// </summary>
    // ReSharper disable once StaticMemberInGenericType
    private static readonly StagedBroadcastPlan<TEvent>? Plan =
        GeneratedDispatchRoots.FindBroadcastPlan(typeof(TEvent)) as StagedBroadcastPlan<TEvent>;

    private const int NoPipelineMode = 0;
    private const int StraightMode = 1;
    private const int StagedMode = 2;
    private const int PlanMode = 3;
    private const int PlanDirectMode = 4;

    private readonly IMessageDependenciesFactory _factory;

    /// <summary>The group-less delivery, decided once at construction; see the class remarks.</summary>
    private readonly int _mode;

    /// <summary>The group-less composition; null only in <see cref="NoPipelineMode"/>.</summary>
    private readonly IMessageDependencies? _dependencies;

    /// <summary>The composition as its concrete type — the bare loop's handler arrays.</summary>
    private readonly MessageDependencies? _fast;

    private GroupedSlot? _cachedGroupedSlot;

    // Public within the internal type: the reflective fallback for unrooted runtime types
    // constructs through Activator, which only binds public constructors.
    public FrozenBroadcastDispatch(IMessageDependenciesFactory dependenciesFactory)
    {
        _factory = dependenciesFactory;

        var dependencies = dependenciesFactory.Find(typeof(TEvent), EmptyGroups);

        if (dependencies is null)
        {
            // A message no compiled composition serves. The mode is the whole answer; the
            // caller's throw-if-none flag decides what it means, per publish.
            _mode = NoPipelineMode;
            return;
        }

        _dependencies = dependencies;
        _fast = dependencies as MessageDependencies;

        if (Plan is not null
            && _fast is { MemoizedInstances: false }
            && StagedPlanGate.Matches(_fast, Plan.Composition))
        {
            _mode = Plan.SupportsDirectConstruction
                    && dependenciesFactory is MessageDependenciesFactory typedFactory
                    && StagedPlanGate.AllPlainTransient(typedFactory, Plan.Composition)
                ? PlanDirectMode
                : PlanMode;
            return;
        }

        _mode = _fast is { HasNoInterceptors: true } ? StraightMode : StagedMode;
    }

    /// <inheritdoc />
    internal override ValueTask Publish(
        object message,
        ErgosfareContext context,
        IServiceProvider serviceProvider,
        IEnumerable<string>? groups,
        bool throwIfNoHandlerFound)
        => PublishCore(message, context, serviceProvider, groups, throwIfNoHandlerFound);

    /// <inheritdoc />
    internal override ValueTask PublishPooled(
        object message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        IEnumerable<string>? groups,
        bool throwIfNoHandlerFound)
    {
        var context = ErgosfareContextPool.Rent(null, cancellationToken);
        ValueTask task;

        try
        {
            task = PublishCore(message, context, serviceProvider, groups, throwIfNoHandlerFound);
        }
        catch
        {
            ErgosfareContextPool.Return(context);
            throw;
        }

        // Synchronously completed publishes (the common case) return the context inline —
        // no async state machine on the hot path. Only a genuinely suspended pipeline pays
        // for the awaiting helper.
        if (task.IsCompletedSuccessfully)
        {
            ErgosfareContextPool.Return(context);
            return default;
        }

        return AwaitAndReturn(task, context);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask AwaitAndReturn(ValueTask task, ErgosfareContext context)
        {
            try
            {
                await task;
            }
            finally
            {
                ErgosfareContextPool.Return(context);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask PublishCore(
        object message,
        ErgosfareContext context,
        IServiceProvider serviceProvider,
        IEnumerable<string>? groups,
        bool throwIfNoHandlerFound)
    {
        if (groups is not (null or List<string> { Count: 0 } or string[] { Length: 0 } or GroupSet { Count: 0 }))
        {
            return PublishGrouped(message, context, serviceProvider, groups, throwIfNoHandlerFound);
        }

        // Ordered by heat: the interceptorless broadcast is the lane the whole design
        // protects ("hooks cost zero while not attached"), the plan arms carry the
        // interceptor-bearing pipelines, the staged body is the runtime filling for
        // compositions no plan claimed.
        var mode = _mode;

        if (mode == StraightMode)
        {
            return PublishStraightThrough((TEvent)message, _fast!, context, serviceProvider, throwIfNoHandlerFound);
        }

        if (mode >= PlanMode)
        {
            return mode == PlanDirectMode
                ? Plan!.ExecuteDirect((TEvent)message, context, serviceProvider)
                : Plan!.Execute((TEvent)message, context, serviceProvider);
        }

        if (mode == StagedMode)
        {
            return PublishThroughStages((TEvent)message, _dependencies!, context, serviceProvider, throwIfNoHandlerFound);
        }

        return NoPipeline(throwIfNoHandlerFound);
    }

    /// <summary>
    /// The group-filtered publish: the composition the filter selects, delivered through
    /// the same two bodies. A single last-used slot serves the overwhelmingly common shape
    /// — one stable group set per message type — with an ordinal element-wise compare; a
    /// miss rebuilds through the factory's own per-(type, set) cache, so alternating sets
    /// stay cheap and every distinct set gets its own frozen composition.
    /// </summary>
    private ValueTask PublishGrouped(
        object message,
        ErgosfareContext context,
        IServiceProvider serviceProvider,
        IEnumerable<string> groups,
        bool throwIfNoHandlerFound)
    {
        var slot = _cachedGroupedSlot;

        // Deliberate: groups is matched allocation-free first and only materialized on a miss.
        // ReSharper disable once PossibleMultipleEnumeration
        if (slot is null || !GroupSlotMatch.Matches(groups, slot.Groups, slot.Canonical))
        {
            // A canonical set contributes its immutable name array directly.
            var canonical = groups as GroupSet;
            // ReSharper disable once PossibleMultipleEnumeration
            var materialized = canonical?.Names ?? [.. groups];
            var dependencies = _factory.Find(typeof(TEvent), materialized);

            if (dependencies is null)
            {
                // Not slotted: the catalog already caches its own negative lookup per type,
                // so the repeat cost is a dictionary hit — and an empty slot cannot be
                // confused with a served set.
                return NoPipeline(throwIfNoHandlerFound);
            }

            var admitted = AdmitGroupedPlan(materialized, dependencies, out var planDirect);
            slot = new GroupedSlot(materialized, canonical, dependencies, admitted, planDirect);
            _cachedGroupedSlot = slot;
        }

        if (slot.Plan is { } plan)
        {
            return slot.PlanDirect
                ? plan.ExecuteDirect((TEvent)message, context, serviceProvider)
                : plan.Execute((TEvent)message, context, serviceProvider);
        }

        return slot.Fast is { HasNoInterceptors: true } fast
            ? PublishStraightThrough((TEvent)message, fast, context, serviceProvider, throwIfNoHandlerFound)
            : PublishThroughStages((TEvent)message, slot.Dependencies, context, serviceProvider, throwIfNoHandlerFound);
    }

    /// <summary>
    /// The compiled plan for one group set, admitted against that set's own composition —
    /// the same question the group-less arm answers in the constructor, asked once per set
    /// when its slot is first filled rather than per publish.
    /// </summary>
    private StagedBroadcastPlan<TEvent>? AdmitGroupedPlan(
        string[] groups, IMessageDependencies dependencies, out bool direct)
    {
        direct = false;

        if (GeneratedDispatchRoots.FindBroadcastPlan(typeof(TEvent), groups) is not StagedBroadcastPlan<TEvent> plan
            || dependencies is not MessageDependencies { MemoizedInstances: false } fast
            || !StagedPlanGate.Matches(fast, plan.Composition))
        {
            return null;
        }

        direct = plan.SupportsDirectConstruction
                 && _factory is MessageDependenciesFactory typedFactory
                 && StagedPlanGate.AllPlainTransient(typedFactory, plan.Composition);

        return plan;
    }

    /// <summary>
    /// Broadcasts sequentially over the direct then indirect handler arrays without any
    /// async machinery while handlers complete synchronously; the first suspension hands
    /// the remainder to an awaiting helper, preserving strict sequential order. Exceptions
    /// propagate raw, and an empty pipeline throws only when
    /// <paramref name="throwIfNoHandlerFound"/> asks for it.
    /// </summary>
    private static ValueTask PublishStraightThrough(
        TEvent message,
        MessageDependencies plan,
        ErgosfareContext context,
        IServiceProvider serviceProvider,
        bool throwIfNoHandlerFound)
    {
        var direct = plan.HandlerArray;
        var indirect = plan.IndirectHandlerArray;

        if (direct.Length == 0 && indirect.Length == 0)
        {
            return throwIfNoHandlerFound
                ? ValueTask.FromException(new NoHandlerFoundException(typeof(TEvent)))
                : default;
        }

        for (var i = 0; i < direct.Length; i++)
        {
            var pending = Invoke(direct[i], message, context, serviceProvider);

            if (!pending.IsCompletedSuccessfully)
            {
                return AwaitRemaining(pending, message, plan, context, serviceProvider, i + 1, inIndirect: false);
            }
        }

        for (var i = 0; i < indirect.Length; i++)
        {
            var pending = Invoke(indirect[i], message, context, serviceProvider);

            if (!pending.IsCompletedSuccessfully)
            {
                return AwaitRemaining(pending, message, plan, context, serviceProvider, i + 1, inIndirect: true);
            }
        }

        return default;

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask AwaitRemaining(
            ValueTask pending, TEvent message, MessageDependencies plan, ErgosfareContext context,
            IServiceProvider serviceProvider, int next, bool inIndirect)
        {
            await pending;

            var direct = plan.HandlerArray;
            var indirect = plan.IndirectHandlerArray;

            if (!inIndirect)
            {
                for (var i = next; i < direct.Length; i++)
                {
                    await Invoke(direct[i], message, context, serviceProvider);
                }

                next = 0;
            }

            for (var i = next; i < indirect.Length; i++)
            {
                await Invoke(indirect[i], message, context, serviceProvider);
            }
        }
    }

    /// <summary>
    /// The full interceptor-bearing broadcast: pre stages (which may replace the event),
    /// every handler in order, post stages over the resultless <see cref="Unit"/> slot,
    /// exception stages that swallow only when one actually matched, and final stages an
    /// abort skips.
    /// </summary>
    private static async ValueTask PublishThroughStages(
        TEvent message,
        IMessageDependencies dependencies,
        ErgosfareContext context,
        IServiceProvider serviceProvider,
        bool throwIfNoHandlerFound)
    {
        var handlers = dependencies.Handlers;
        var indirectHandlers = dependencies.IndirectHandlers;

        if (handlers.Count == 0 && indirectHandlers.Count == 0)
        {
            if (throwIfNoHandlerFound)
            {
                throw new NoHandlerFoundException(typeof(TEvent));
            }

            return;
        }

        Exception? exception = null;
        var aborted = false;

        try
        {
            // Empty stages are skipped outright — an invoker pass over an empty stage is a
            // no-op, so the guards only cut dead work, not behavior.
            if (dependencies.PreInterceptors.Count > 0)
            {
                // Pre-interceptors may transform the event — including returning a brand new
                // instance — so the broadcast continues with the returned one, exactly as the
                // single-handler pipelines do. Events carry no result adapter.
                message = (TEvent)await PreInterceptorInvocationStrategy<TEvent>.Invoke(
                    dependencies, serviceProvider, message, context);
            }

            for (var i = 0; i < handlers.Count; i++)
            {
                await Invoke(handlers[i], message, context, serviceProvider);
            }

            for (var i = 0; i < indirectHandlers.Count; i++)
            {
                await Invoke(indirectHandlers[i], message, context, serviceProvider);
            }

            if (dependencies.PostInterceptors.Count > 0)
            {
                // A publish produces nothing, so the result slot carries the one value a
                // resultless pipeline has — the same Unit the void plans hand their stages.
                _ = await PostInterceptorInvocationStrategy<TEvent, Unit>.Invoke(
                    dependencies, null, serviceProvider, message, Unit.Value, context);
            }
        }
        catch (ExecutionAbortedException)
        {
            // A participant stopped the publish. Nothing else runs — not the exception
            // stage, not the final stage — and the signal continues to the publisher.
            aborted = true;
            throw;
        }
        catch (Exception e)
        {
            exception = e;

            // Zero exception interceptors: rethrow directly. Final interceptors still run
            // from the finally block.
            if (dependencies.ExceptionInterceptors.Count == 0)
            {
                throw;
            }

            var (matched, _) = await ExceptionInterceptorInvocationStrategy<TEvent, Unit>.Invoke(
                dependencies, serviceProvider, message, Unit.Value, e, context);

            // Every registered interceptor filtered the exception out — nothing handled it,
            // so it propagates with its original stack.
            if (!matched)
            {
                throw;
            }
        }
        finally
        {
            if (dependencies.FinalInterceptors.Count > 0 && !aborted)
            {
                await FinalInterceptorInvocationStrategy<TEvent, Unit>.Invoke(
                    dependencies, serviceProvider, message, Unit.Value, exception, context);
            }
        }
    }

    /// <summary>
    /// Invokes one handler through its typed contract — the same dispatch rules the
    /// single-handler pipelines apply.
    /// </summary>
    private static ValueTask Invoke(
        IHandlerReference<Core.Abstractions.Handlers.IHandler> reference,
        TEvent message,
        ErgosfareContext context,
        IServiceProvider serviceProvider)
    {
        var handler = reference.Resolve(serviceProvider);

        switch (handler)
        {
            case Core.Abstractions.Handlers.IAsyncHandler<TEvent> asyncHandler:
                return asyncHandler.HandleAsync(message, context);
            case Core.Abstractions.Handlers.IHandler<TEvent, ValueTask> valueTaskShaped:
                return valueTaskShaped.Handle(message, context);
            case Core.Abstractions.Handlers.IHandler<TEvent, object> syncHandler:
                syncHandler.Handle(message, context);
                return ValueTask.CompletedTask;
            default:
                throw new NotSupportedException(
                    $"'{handler.GetType()}' does not implement a supported handler contract for event '{typeof(TEvent)}'. " +
                    "Interface-erased dispatch is not supported; publish with the concrete event type.");
        }
    }

    /// <summary>
    /// The publish outcome for an event nothing will handle: the caller's flag decides,
    /// and it decides the same way whether the event type is unregistered or merely
    /// unhandled.
    /// </summary>
    private static ValueTask NoPipeline(bool throwIfNoHandlerFound)
        => throwIfNoHandlerFound
            ? ValueTask.FromException(new NoHandlerFoundException(typeof(TEvent)))
            : default;

    private sealed class GroupedSlot(
        string[] groups,
        GroupSet? canonical,
        IMessageDependencies dependencies,
        StagedBroadcastPlan<TEvent>? plan,
        bool planDirect)
    {
        public readonly string[] Groups = groups;
        public readonly GroupSet? Canonical = canonical;
        public readonly IMessageDependencies Dependencies = dependencies;
        public readonly MessageDependencies? Fast = dependencies as MessageDependencies;

        /// <summary>The compiled plan of this group set, or <c>null</c> when none serves it.</summary>
        public readonly StagedBroadcastPlan<TEvent>? Plan = plan;

        /// <summary>Whether the plan's direct-construction variant qualifies for this container.</summary>
        public readonly bool PlanDirect = planDirect;
    }
}
