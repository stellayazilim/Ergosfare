using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;
using Stella.Ergosfare.Core.Internal.Mediator;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events;

internal sealed class EventBroadcastInvoker<TEvent> : IEventBroadcastInvoker
    where TEvent : notnull
{
    // One copy per closed event type is deliberate — the invoker itself is per-event-type.
    // ReSharper disable once StaticMemberInGenericType
    private static readonly string[] EmptyGroups = [];

    /// <summary>
    /// Shared strategy for publishes without caller-supplied settings. Safe to share: the
    /// settings instance is private and never mutated, and the strategy keeps all
    /// per-publish state in locals — one instance serves concurrent publishes.
    /// </summary>
    // ReSharper disable once StaticMemberInGenericType
    private static readonly EventMediationSettings DefaultSettings = new();
    private static readonly AsyncBroadcastMediationStrategy<TEvent> DefaultStrategy = new(DefaultSettings);

    /// <summary>
    /// Publishes against the dispatch engine: the concrete machinery is known by
    /// construction, so every publish runs the fast lane against the invoker-cached
    /// pipeline plan and the caller's scope provider — no per-publish composition lookup.
    /// An externally owned context (nested publish) is used as-is; the caller controls its
    /// lifetime, so nothing is rented and nothing is returned.
    /// </summary>
    public ValueTask Publish(object @event, EventMediationSettings? settings, CancellationToken cancellationToken,
        MessageDispatchEngine engine, IServiceProvider serviceProvider,
        ErgosfareContext? externalContext = null,
        IEnumerable<string>? groupsOverride = null)
    {
        var pooled = externalContext is null
            ? ErgosfareContextPool.Rent(settings?.Items, cancellationToken)
            : null;
        var context = externalContext ?? pooled!;

        ValueTask task;

        try
        {
            var groups = groupsOverride ?? settings?.Filters.Groups;
            var dependencies = groups is null or List<string> { Count: 0 } or string[] { Length: 0 } or GroupSet { Count: 0 }
                ? GetPlan(engine.DependenciesFactory)
                : GetGroupedPlan(engine.DependenciesFactory, groups);

            // Straight-through broadcast: with no interceptor stages and no handler
            // filtering requested, loop the handler arrays directly — synchronously while
            // handlers complete synchronously, bailing to an awaiting helper on the first
            // suspension. No strategy or per-stage async frames.
            if (dependencies is null)
            {
                task = NoPipeline(settings?.ThrowIfNoHandlerFound ?? false);
            }
            else if (dependencies is MessageDependencies { HasNoInterceptors: true } plan
                && (settings is null
                    || ReferenceEquals(settings.Filters.HandlerPredicate,
                        EventMediationSettings.EventMediationFilters.AcceptAllHandlers)))
            {
                task = PublishStraightThrough(
                    (TEvent)@event, plan, context, serviceProvider,
                    settings?.ThrowIfNoHandlerFound ?? false);
            }
            else
            {
                // Null settings (the common publish) reuse the cached default strategy — no
                // EventMediationSettings, no Filters/List/Dictionary, no strategy allocation.
                var strategy = settings is null
                    ? DefaultStrategy
                    : new AsyncBroadcastMediationStrategy<TEvent>(settings);

                task = strategy.Mediate((TEvent)@event, dependencies, context, serviceProvider);
            }
        }
        catch
        {
            if (pooled is not null)
            {
                ErgosfareContextPool.Return(pooled);
            }

            throw;
        }

        if (pooled is null)
        {
            return task;
        }

        if (task.IsCompletedSuccessfully)
        {
            ErgosfareContextPool.Return(pooled);
            return default;
        }

        return AwaitAndReturn(task, pooled);

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
    /// The group-less pipeline plan for <typeparamref name="TEvent"/>, cached on the
    /// invoker. A composition is settled before the container is built, so the plan never
    /// goes stale. Races are benign: concurrent writers publish equivalent state.
    /// </summary>
    private IMessageDependencies? _cachedDependencies;
    private MessageDependenciesFactory? _cachedFactory;

    private IMessageDependencies? GetPlan(
        Core.Abstractions.Factories.IMessageDependenciesFactory dependenciesFactory)
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            var cached = _cachedDependencies;

            // The invoker is process-wide (one per event type) while factories are
            // per-container — the factory reference is part of the cache key, so one
            // container's plan (which may pin that container's root provider for
            // memoized, all-singleton pipelines) is never served to another container.
            // Multiple containers ping-ponging simply rebuild the single slot; the
            // factory's own per-container cache keeps that cheap.
            if (cached is not null && ReferenceEquals(_cachedFactory, typedFactory))
            {
                return cached;
            }

            var dependencies = BuildPlan(typedFactory, EmptyGroups);

            if (dependencies is null)
            {
                // A message with no composition is not cached here: the catalog already
                // caches its own negative lookup per type, so the repeat cost is a
                // dictionary hit — and leaving the slot untouched keeps the cache fields
                // consistent for concurrent readers, which a "cached null" could not.
                return null;
            }

            _cachedDependencies = dependencies;
            _cachedFactory = typedFactory;
            return dependencies;
        }

        return BuildPlan(dependenciesFactory, EmptyGroups);
    }

    /// <summary>
    /// Grouped counterpart of <see cref="GetPlan"/>: a single last-used
    /// (factory, group set) slot serves the overwhelmingly common shape — one stable
    /// group set per event type — with an ordinal element-wise compare instead of a
    /// per-publish key materialization. The dependencies come from the same factory call
    /// the Mediate path would make, so the group-filtered pipeline is identical; a slot
    /// miss (alternating group sets, another container) only rebuilds
    /// through the factory's own process-wide dependency cache. The slot snapshots the
    /// caller's group sequence, so later caller-side mutation of a reused settings
    /// instance is seen as a different group set, never as a stale hit.
    /// </summary>
    private GroupedPlanSlot? _cachedGroupedPlan;

    private sealed class GroupedPlanSlot(
        MessageDependenciesFactory factory,
        string[] groups,
        GroupSet? canonical,
        IMessageDependencies dependencies)
    {
        public readonly MessageDependenciesFactory Factory = factory;
        public readonly string[] Groups = groups;
        public readonly GroupSet? Canonical = canonical;
        public readonly IMessageDependencies Dependencies = dependencies;
    }

    private IMessageDependencies? GetGroupedPlan(
        Core.Abstractions.Factories.IMessageDependenciesFactory dependenciesFactory,
        IEnumerable<string> groups)
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            var slot = _cachedGroupedPlan;

            // Deliberate: groups is matched allocation-free first and only materialized on a slot miss.
            // ReSharper disable once PossibleMultipleEnumeration
            if (slot is not null
                && ReferenceEquals(slot.Factory, typedFactory)
                && PipelineExecutorCache.SlotMatches(groups, slot.Groups, slot.Canonical))
            {
                return slot.Dependencies;
            }

            // A canonical set contributes its immutable name array directly.
            var canonical = groups as GroupSet;
            // ReSharper disable once PossibleMultipleEnumeration
            var materialized = canonical?.Names ?? [.. groups];
            var dependencies = BuildPlan(typedFactory, materialized);

            if (dependencies is null)
            {
                // Not slotted, for the reason GetPlan gives.
                return null;
            }

            _cachedGroupedPlan = new GroupedPlanSlot(
                typedFactory, materialized, canonical, dependencies);
            return dependencies;
        }

        return BuildPlan(dependenciesFactory, [.. groups]);
    }

    /// <summary>
    /// Broadcasts sequentially over the direct then indirect handler arrays without any
    /// async machinery while handlers complete synchronously; the first suspension hands
    /// the remainder to an awaiting helper, preserving strict sequential order. Semantics
    /// match <see cref="AsyncBroadcastMediationStrategy{TMessage}"/> for the
    /// zero-interceptor, unfiltered case: exceptions propagate raw, and an empty pipeline
    /// throws only when <paramref name="throwIfNoHandlerFound"/> asks for it.
    /// </summary>
    private static ValueTask PublishStraightThrough(
        TEvent @event,
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
            var pending = Invoke(direct[i], @event, context, serviceProvider);

            if (!pending.IsCompletedSuccessfully)
            {
                return AwaitRemaining(pending, @event, plan, context, serviceProvider, i + 1, inIndirect: false);
            }
        }

        for (var i = 0; i < indirect.Length; i++)
        {
            var pending = Invoke(indirect[i], @event, context, serviceProvider);

            if (!pending.IsCompletedSuccessfully)
            {
                return AwaitRemaining(pending, @event, plan, context, serviceProvider, i + 1, inIndirect: true);
            }
        }

        return default;

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask AwaitRemaining(
            ValueTask pending, TEvent @event, MessageDependencies plan, ErgosfareContext context,
            IServiceProvider serviceProvider, int next, bool inIndirect)
        {
            await pending;

            var direct = plan.HandlerArray;
            var indirect = plan.IndirectHandlerArray;

            if (!inIndirect)
            {
                for (var i = next; i < direct.Length; i++)
                {
                    await Invoke(direct[i], @event, context, serviceProvider);
                }

                next = 0;
            }

            for (var i = next; i < indirect.Length; i++)
            {
                await Invoke(indirect[i], @event, context, serviceProvider);
            }
        }
    }

    /// <summary>
    /// Invokes one handler through its typed contract — the same dispatch rules the
    /// broadcast strategy applies.
    /// </summary>
    private static ValueTask Invoke(
        IHandlerReference<Core.Abstractions.Handlers.IHandler> reference,
        TEvent @event,
        ErgosfareContext context,
        IServiceProvider serviceProvider)
    {
        var handler = reference.Resolve(serviceProvider);

        switch (handler)
        {
            case Core.Abstractions.Handlers.IAsyncHandler<TEvent> asyncHandler:
                return asyncHandler.HandleAsync(@event, context);
            case Core.Abstractions.Handlers.IHandler<TEvent, ValueTask> valueTaskShaped:
                return valueTaskShaped.Handle(@event, context);
            case Core.Abstractions.Handlers.IHandler<TEvent, object> syncHandler:
                syncHandler.Handle(@event, context);
                return ValueTask.CompletedTask;
            default:
                throw new NotSupportedException(
                    $"'{handler.GetType()}' does not implement a supported handler contract for event '{typeof(TEvent)}'. " +
                    "Interface-erased dispatch is not supported; publish with the concrete event type.");
        }
    }

    /// <summary>
    /// The pipeline plan for <typeparamref name="TEvent"/>, or <c>null</c> when the event
    /// type has no descriptor at all.
    /// </summary>
    /// <remarks>
    /// Null rather than an exception on purpose: an unregistered event type is the same
    /// "nothing will handle this" as a registered one whose handlers are all filtered out,
    /// and both are the caller's <see cref="EventMediationSettings.ThrowIfNoHandlerFound"/>
    /// to answer. Only the publish site knows what the caller asked for, so the decision
    /// belongs there and not here.
    /// </remarks>
    private static IMessageDependencies? BuildPlan(
        Core.Abstractions.Factories.IMessageDependenciesFactory factory,
        string[] groups)
        // Non-throwing by design: an event nobody subscribes to has no pipeline, and the
        // caller's throwIfNoHandlerFound flag — not this lookup — decides what that means.
        => factory.Find(typeof(TEvent), groups);

    /// <summary>
    /// The publish outcome for an event nothing will handle: the caller's flag decides,
    /// and it decides the same way whether the event type is unregistered or merely
    /// unhandled.
    /// </summary>
    private static ValueTask NoPipeline(bool throwIfNoHandlerFound)
        => throwIfNoHandlerFound
            ? ValueTask.FromException(new NoHandlerFoundException(typeof(TEvent)))
            : default;
}
