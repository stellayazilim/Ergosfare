using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// <see cref="FrozenBroadcastDispatch"/> closed over one event type.
/// </summary>
/// <typeparam name="TEvent">The event type this dispatch publishes.</typeparam>
/// <remarks>
/// <para>
/// Everything is decided in the constructor: the ungrouped participants are resolved once,
/// the compiled plan is accepted or rejected against them once, and what comes out is a
/// mode that never changes. No publish consults a gate or resolves participants. Deciding
/// this early is safe because the composition is settled before the container is built, so
/// the constructor sees exactly what the first publish would.
/// </para>
/// <para>
/// The delivery bodies live here too: a plain loop when there are no interceptors, and the
/// full staged body when there are. A pipeline with a single handler is just the one-handler
/// case of those bodies.
/// </para>
/// </remarks>
internal sealed class FrozenBroadcastDispatch<TEvent> : FrozenBroadcastDispatch
    where TEvent : notnull
{
    // One copy per closed message type is deliberate — the dispatch itself is per type.
    // ReSharper disable once StaticMemberInGenericType

    /// <summary>
    /// The plan compiled for this event type, or <c>null</c> when the generator produced
    /// none — an event with no interceptors, or one whose subscribers it could not model
    /// exactly.
    /// </summary>
    // ReSharper disable once StaticMemberInGenericType
    private static readonly StagedBroadcastPlan<TEvent>? Plan =
        GeneratedDispatchRoots.FindBroadcastPlan(typeof(TEvent)) as StagedBroadcastPlan<TEvent>;

    /// <summary>
    /// No composition serves this event type, so a publish reaches nobody.
    /// </summary>
    private const int NoPipelineMode = 0;

    /// <summary>
    /// No interceptors: loop over the handlers directly.
    /// </summary>
    private const int StraightMode = 1;

    /// <summary>
    /// Interceptors are present and no plan covers them: run the full staged body.
    /// </summary>
    private const int StagedMode = 2;

    /// <summary>
    /// Run the compiled plan.
    /// </summary>
    private const int PlanMode = 3;

    /// <summary>
    /// Run the compiled plan and let it construct participants itself.
    /// </summary>
    private const int PlanDirectMode = 4;

    private readonly IMessageDependenciesFactory _factory;

    /// <summary>
    /// How an ungrouped publish is delivered, decided once in the constructor.
    /// </summary>
    private readonly int _mode;

    /// <summary>
    /// The ungrouped participants; <c>null</c> only in <see cref="NoPipelineMode"/>.
    /// </summary>
    private readonly IMessageDependencies? _dependencies;

    /// <summary>
    /// The same participants as their concrete type, which is what the plain loop indexes.
    /// </summary>
    private readonly MessageDependencies? _fast;

    private GroupedSlot? _cachedGroupedSlot;

    /// <summary>
    /// Resolves this event's participants and settles how it will be published.
    /// </summary>
    /// <param name="dependenciesFactory">The factory participants are resolved through.</param>
    /// <remarks>
    /// Public despite the type being internal: the reflective fallback for event types
    /// without a generated root constructs through <c>Activator</c>, which only binds public
    /// constructors.
    /// </remarks>
    public FrozenBroadcastDispatch(IMessageDependenciesFactory dependenciesFactory)
    {
        _factory = dependenciesFactory;

        var dependencies = dependenciesFactory.Find(typeof(TEvent), []);

        if (dependencies is null)
        {
            // No composition serves this event type. Publishing it reaches nobody, which is
            // not an error at run time.
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
        IEnumerable<string>? groups)
        => PublishCore(message, context, serviceProvider, groups);

    /// <inheritdoc />
    internal override ValueTask PublishPooled(
        object message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        IEnumerable<string>? groups)
    {
        var context = ErgosfareContextPool.Rent(null, cancellationToken);
        ValueTask task;

        try
        {
            task = PublishCore(message, context, serviceProvider, groups);
        }
        catch
        {
            ErgosfareContextPool.Return(context);
            throw;
        }

        // A publish that finished synchronously — the common case — releases its context
        // here, so no async state machine is built for it. Only one that actually suspended
        // pays for the helper below.
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

    /// <summary>
    /// Delivers a publish through whichever body its mode names.
    /// </summary>
    /// <param name="message">The event to publish.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <param name="serviceProvider">The provider handlers are resolved from.</param>
    /// <param name="groups">The groups to deliver to, or <c>null</c> for the default.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask PublishCore(
        object message,
        ErgosfareContext context,
        IServiceProvider serviceProvider,
        IEnumerable<string>? groups)
    {
        if (groups is not (null or List<string> { Count: 0 } or string[] { Length: 0 } or GroupSet { Count: 0 }))
        {
            return PublishGrouped(message, context, serviceProvider, groups);
        }

        // Ordered by how often each is taken: an event with no interceptors is the case the
        // whole design protects, the plan arms carry the intercepted pipelines, and the
        // staged body serves the compositions no plan covered.
        var mode = _mode;

        if (mode == StraightMode)
        {
            return PublishStraightThrough((TEvent)message, _fast!, context, serviceProvider);
        }

        if (mode >= PlanMode)
        {
            return mode == PlanDirectMode
                ? Plan!.ExecuteDirect((TEvent)message, context, serviceProvider)
                : Plan!.Execute((TEvent)message, context, serviceProvider);
        }

        if (mode == StagedMode)
        {
            return PublishThroughStages((TEvent)message, _dependencies!, context, serviceProvider);
        }

        return NoPipeline();
    }

    /// <summary>
    /// Delivers a publish that named groups, to the handlers those groups select.
    /// </summary>
    /// <param name="message">The event to publish.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <param name="serviceProvider">The provider handlers are resolved from.</param>
    /// <param name="groups">The groups the publish asked for.</param>
    /// <returns>A task that completes when every matching handler has run.</returns>
    /// <remarks>
    /// A single last-used slot serves the common shape of one stable group set per event
    /// type; a miss rebuilds through the factory's own per-(type, set) cache, so alternating
    /// sets stay cheap and each set gets its own settled participants.
    /// </remarks>
    private ValueTask PublishGrouped(
        object message,
        ErgosfareContext context,
        IServiceProvider serviceProvider,
        IEnumerable<string> groups)
    {
        var slot = _cachedGroupedSlot;

        // Deliberate: groups is compared without being copied first, and copied only on a
        // miss.
        // ReSharper disable once PossibleMultipleEnumeration
        if (slot is null || !GroupSlotMatch.Matches(groups, slot.Groups, slot.Canonical))
        {
            // A canonical set hands over its own immutable array, so it costs no copy.
            var canonical = groups as GroupSet;
            // ReSharper disable once PossibleMultipleEnumeration
            var materialized = canonical?.Names ?? [.. groups];
            var dependencies = _factory.Find(typeof(TEvent), materialized);

            if (dependencies is null)
            {
                // Not slotted: the catalog caches its own misses per type, so asking again
                // costs a dictionary lookup — and an empty slot could not be told apart from
                // a set that is served.
                return NoPipeline();
            }

            var admitted = AdmitGroupedPlan(materialized, dependencies, out var planDirect);
            slot = new GroupedSlot(materialized, canonical, dependencies, admitted, planDirect);
            _cachedGroupedSlot = slot;
        }

        if (slot.GroupedPlan is { } plan)
        {
            // A plan compiled for this exact set already knows its participants; the
            // filtering plan works them out from the set it is handed.
            if (plan.FilterGroups is not null)
            {
                return slot.PlanDirect
                    ? plan.ExecuteFilteredDirect((TEvent)message, context, serviceProvider, slot.Groups)
                    : plan.ExecuteFiltered((TEvent)message, context, serviceProvider, slot.Groups);
            }

            return slot.PlanDirect
                ? plan.ExecuteDirect((TEvent)message, context, serviceProvider)
                : plan.Execute((TEvent)message, context, serviceProvider);
        }

        return slot.Fast is { HasNoInterceptors: true } fast
            ? PublishStraightThrough((TEvent)message, fast, context, serviceProvider)
            : PublishThroughStages((TEvent)message, slot.Dependencies, context, serviceProvider);
    }

    /// <summary>
    /// Decides which compiled plan, if any, may serve one group set.
    /// </summary>
    /// <param name="groups">The group set being decided for.</param>
    /// <param name="dependencies">The participants that set selects.</param>
    /// <param name="direct">
    /// Set to <c>true</c> when the plan may construct participants itself.
    /// </param>
    /// <returns>The plan, or <c>null</c> when none may serve the set.</returns>
    /// <remarks>
    /// The same question the constructor answers for the default set, asked once per set as
    /// its slot is filled rather than on every publish.
    /// </remarks>
    private StagedBroadcastPlan<TEvent>? AdmitGroupedPlan(
        string[] groups, IMessageDependencies dependencies, out bool direct)
    {
        direct = false;

        var plan = GeneratedDispatchRoots.FindBroadcastPlan(typeof(TEvent), groups) as StagedBroadcastPlan<TEvent>;

        if (plan is not null)
        {
            if (dependencies is not MessageDependencies { MemoizedInstances: false } keyed
                || !StagedPlanGate.Matches(keyed, plan.Composition))
            {
                return null;
            }
        }
        else
        {
            // No plan was compiled for this set — every call site passed its groups as a
            // runtime value, or nobody ever spelled this particular set. The filtering plan
            // can serve any set, but only if it is checked against the participants of the
            // groups it covers: that is the one set reproducing everything its body holds.
            plan = GeneratedDispatchRoots.FindFilteredBroadcastPlan(typeof(TEvent)) as StagedBroadcastPlan<TEvent>;

            if (plan?.FilterGroups is not { } covered
                || _factory.Find(typeof(TEvent), covered) is not MessageDependencies { MemoizedInstances: false } full
                || !StagedPlanGate.Matches(full, plan.Composition))
            {
                return null;
            }
        }

        direct = plan.SupportsDirectConstruction
                 && _factory is MessageDependenciesFactory typedFactory
                 && StagedPlanGate.AllPlainTransient(typedFactory, plan.Composition);

        return plan;
    }

    /// <summary>
    /// Delivers the event to each handler in turn, direct handlers before covariant ones.
    /// </summary>
    /// <param name="message">The event to publish.</param>
    /// <param name="plan">The participants to deliver to.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <param name="serviceProvider">The provider handlers are resolved from.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    /// <remarks>
    /// No async machinery is used while handlers keep completing synchronously; the first
    /// one that suspends hands the remainder to a helper, which keeps the order strictly
    /// sequential either way. Failures propagate untouched, and an event with no handlers
    /// simply completes.
    /// </remarks>
    private static ValueTask PublishStraightThrough(
        TEvent message,
        MessageDependencies plan,
        ErgosfareContext context,
        IServiceProvider serviceProvider)
    {
        var direct = plan.HandlerArray;
        var indirect = plan.IndirectHandlerArray;

        if (direct.Length == 0 && indirect.Length == 0)
        {
            // Reaching nobody is not raised here: a publish that no subscriber in the
            // compilation serves already fails the build (ERGO005), so what is left at run
            // time is the selection this container made.
            return default;
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
    /// Delivers the event through the full set of interceptor stages.
    /// </summary>
    /// <param name="message">The event to publish.</param>
    /// <param name="dependencies">The participants to run.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    /// <exception cref="ExecutionAbortedException">A participant stopped the publish.</exception>
    private static async ValueTask PublishThroughStages(
        TEvent message,
        IMessageDependencies dependencies,
        ErgosfareContext context,
        IServiceProvider serviceProvider)
    {
        var handlers = dependencies.Handlers;
        var indirectHandlers = dependencies.IndirectHandlers;

        if (handlers.Count == 0 && indirectHandlers.Count == 0)
        {
            return;
        }

        Exception? exception = null;
        var aborted = false;

        try
        {
            // Empty stages are skipped outright. Running one changes nothing, so these
            // guards only cut work.
            if (dependencies.PreInterceptors.Count > 0)
            {
                // A pre-interceptor may replace the event entirely, and the publish
                // continues with what it returned — as the single-handler pipelines do.
                // Events bind no result adapter.
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
                // A publish produces nothing, so the stages receive the one value a
                // resultless pipeline has.
                _ = await PostInterceptorInvocationStrategy<TEvent, Unit>.Invoke(
                    dependencies, null, serviceProvider, message, Unit.Value, context);
            }
        }
        catch (ExecutionAbortedException)
        {
            // A participant stopped the publish. Nothing else runs — neither the exception
            // stage nor the final stage — and the signal continues to the publisher.
            aborted = true;
            throw;
        }
        catch (Exception e)
        {
            exception = e;

            // With no exception interceptors the failure goes straight out; the final
            // interceptors still run, from the finally block.
            if (dependencies.ExceptionInterceptors.Count == 0)
            {
                throw;
            }

            var (matched, _) = await ExceptionInterceptorInvocationStrategy<TEvent, Unit>.Invoke(
                dependencies, serviceProvider, message, Unit.Value, e, context);

            // Every interceptor filtered the failure out, so nothing handled it and it
            // continues with its original stack.
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
    /// Calls one handler through whichever main-handler contract it implements.
    /// </summary>
    /// <param name="reference">The handler to resolve and call.</param>
    /// <param name="message">The event to hand it.</param>
    /// <param name="context">The execution context of this publish.</param>
    /// <param name="serviceProvider">The provider the handler is resolved from.</param>
    /// <returns>A task that completes when the handler is done.</returns>
    /// <exception cref="NotSupportedException">
    /// The handler implements no main-handler contract accepting
    /// <typeparamref name="TEvent"/>, which happens when an event is published through a
    /// static type that erases its own.
    /// </exception>
    private static ValueTask Invoke(
        IHandlerReference<Abstractions.Handlers.IHandler> reference,
        TEvent message,
        ErgosfareContext context,
        IServiceProvider serviceProvider)
    {
        var handler = reference.Resolve(serviceProvider);

        switch (handler)
        {
            case Abstractions.Handlers.IAsyncHandler<TEvent> asyncHandler:
                return asyncHandler.HandleAsync(message, context);
            case Abstractions.Handlers.IHandler<TEvent, ValueTask> valueTaskShaped:
                return valueTaskShaped.Handle(message, context);
            case Abstractions.Handlers.IHandler<TEvent, object> syncHandler:
                syncHandler.Handle(message, context);
                return ValueTask.CompletedTask;
            default:
                throw new NotSupportedException(
                    $"'{handler.GetType()}' does not implement a supported handler contract for event '{typeof(TEvent)}'. " +
                    "Interface-erased dispatch is not supported; publish with the concrete event type.");
        }
    }

    /// <summary>
    /// Completes a publish that reaches nobody.
    /// </summary>
    /// <returns>A completed task.</returns>
    /// <remarks>
    /// The outcome is the same whether no composition serves the event type or the groups
    /// asked for select no handler: publishing to nobody is not an error at run time.
    /// </remarks>
    private static ValueTask NoPipeline() => default;

    /// <summary>
    /// One group set together with everything decided for it.
    /// </summary>
    /// <param name="groups">The group names this slot was built for.</param>
    /// <param name="canonical">The canonical set it came from, when it came from one.</param>
    /// <param name="dependencies">The participants for the set.</param>
    /// <param name="plan">The plan serving the set, or <c>null</c>.</param>
    /// <param name="planDirect">Whether that plan may construct participants itself.</param>
    private sealed class GroupedSlot(
        string[] groups,
        GroupSet? canonical,
        IMessageDependencies dependencies,
        StagedBroadcastPlan<TEvent>? plan,
        bool planDirect)
    {
        /// <summary>
        /// The group names this slot was built for.
        /// </summary>
        public readonly string[] Groups = groups;

        /// <summary>
        /// The canonical set this slot was built from, or <c>null</c>.
        /// </summary>
        public readonly GroupSet? Canonical = canonical;

        /// <summary>
        /// The participants for this group set.
        /// </summary>
        public readonly IMessageDependencies Dependencies = dependencies;

        /// <summary>
        /// The same participants as their concrete type, or <c>null</c> when they came from
        /// elsewhere.
        /// </summary>
        public readonly MessageDependencies? Fast = dependencies as MessageDependencies;

        /// <summary>
        /// The plan serving this group set, or <c>null</c> when none does.
        /// </summary>
        public readonly StagedBroadcastPlan<TEvent>? GroupedPlan = plan;

        /// <summary>
        /// Whether the plan may construct participants itself in this container.
        /// </summary>
        public readonly bool PlanDirect = planDirect;
    }
}
