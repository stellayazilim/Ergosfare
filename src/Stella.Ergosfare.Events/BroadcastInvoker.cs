using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Contexts;
using Stella.Ergosfare.Core.Internal.Factories;
using Stella.Ergosfare.Core.Internal.Mediator;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events;

/// <summary>
/// Publishes an event through a pipeline closed over the event's concrete type, so broadcast
/// handlers are always invoked through their typed members — interface-erased publishes
/// (<c>PublishAsync((IEvent)e)</c>) resolve the invoker from the event's runtime type.
/// Invokers are closed once per event type and cached; the per-call
/// <see cref="EventMediationSettings"/> flows into a fresh strategy instance, as before.
/// </summary>
internal interface IEventBroadcastInvoker
{
    ValueTask Publish(object @event, EventMediationSettings? settings, CancellationToken cancellationToken,
        IMessageMediator mediator, ActualTypeOrFirstAssignableTypeMessageResolveStrategy resolveStrategy,
        IResultAdapterService? resultAdapterService, IExecutionContext? externalContext = null);
}

internal sealed class EventBroadcastInvoker<TEvent> : IEventBroadcastInvoker
    where TEvent : notnull
{
    private static readonly string[] EmptyGroups = [];

    /// <summary>
    /// Shared strategy for publishes without caller-supplied settings. Safe to share: the
    /// settings instance is private and never mutated, and the strategy keeps all
    /// per-publish state in locals — one instance serves concurrent publishes.
    /// </summary>
    private static readonly EventMediationSettings DefaultSettings = new();
    private static readonly AsyncBroadcastMediationStrategy<TEvent> DefaultStrategy = new(DefaultSettings);

    public ValueTask Publish(object @event, EventMediationSettings? settings, CancellationToken cancellationToken,
        IMessageMediator mediator, ActualTypeOrFirstAssignableTypeMessageResolveStrategy resolveStrategy,
        IResultAdapterService? resultAdapterService, IExecutionContext? externalContext = null)
    {
        // Null settings (the common publish) reuse the cached default strategy — no
        // EventMediationSettings, no Filters/List/Dictionary, no strategy allocation.
        var strategy = settings is null
            ? DefaultStrategy
            : new AsyncBroadcastMediationStrategy<TEvent>(settings);

        if (externalContext is not null)
        {
            // Caller-owned context (nested publish): the caller controls its lifetime —
            // nothing is rented here, so nothing may be returned here.
            return mediator.Mediate((TEvent)@event, new MediateOptions<TEvent, ValueTask>
            {
                MessageMediationStrategy = strategy,
                MessageResolveStrategy = resolveStrategy,
                CancellationToken = cancellationToken,
                Groups = settings is null ? EmptyGroups : settings.Filters.Groups,
                ExternalContext = externalContext,
            });
        }

        // Root publish: rent a pooled context; this invoker is the completion observer —
        // the broadcast strategy is a plain async ValueTask that awaits every handler and
        // interceptor sequentially, so the returned task's completion really is the end of
        // all context use. Adopting the settings' items dictionary keeps handler writes
        // visible to the caller exactly as the unpooled path did; on return the context
        // detaches the dictionary instead of wiping it.
        var context = ErgosfareExecutionContextPool.Rent(settings?.Items, cancellationToken);

        ValueTask task;

        try
        {
            // Fast lane: for the group-less publish against the concrete mediator, run the
            // broadcast strategy directly against the invoker-cached pipeline plan (the
            // executors' registry-version-guarded pattern) — no MediateOptions, no
            // per-publish descriptor lookup, no Mediate wrapper. Grouped publishes and
            // foreign mediator implementations keep the original Mediate path.
            if ((settings is null || settings.Filters.Groups is List<string> { Count: 0 } or string[] { Length: 0 })
                && mediator is MessageMediator concreteMediator)
            {
                var dependencies = GetPlan(concreteMediator, resolveStrategy);

                // Straight-through broadcast: with no interceptor stages and no handler
                // filtering requested, loop the handler arrays directly — synchronously
                // while handlers complete synchronously, bailing to an awaiting helper on
                // the first suspension. No strategy or per-stage async frames.
                if (dependencies is MessageDependencies { HasNoInterceptors: true } plan
                    && (settings is null
                        || ReferenceEquals(settings.Filters.HandlerPredicate,
                            EventMediationSettings.EventMediationFilters.AcceptAllHandlers)))
                {
                    task = PublishStraightThrough(
                        (TEvent)@event, plan, context, concreteMediator.ScopeProvider,
                        settings?.ThrowIfNoHandlerFound ?? false);
                }
                else
                {
                    task = strategy.Mediate((TEvent)@event, dependencies, context, concreteMediator.ScopeProvider);
                }
            }
            else
            {
                task = mediator.Mediate((TEvent)@event, new MediateOptions<TEvent, ValueTask>
                {
                    MessageMediationStrategy = strategy,
                    MessageResolveStrategy = resolveStrategy,
                    CancellationToken = cancellationToken,
                    Groups = settings is null ? EmptyGroups : settings.Filters.Groups,
                    ExternalContext = context,
                });
            }
        }
        catch
        {
            ErgosfareExecutionContextPool.Return(context);
            throw;
        }

        if (task.IsCompletedSuccessfully)
        {
            ErgosfareExecutionContextPool.Return(context);
            return default;
        }

        return AwaitAndReturn(task, context);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask AwaitAndReturn(ValueTask task, ErgosfareExecutionContext context)
        {
            try
            {
                await task;
            }
            finally
            {
                ErgosfareExecutionContextPool.Return(context);
            }
        }
    }

    /// <summary>
    /// The group-less pipeline plan for <typeparamref name="TEvent"/>, cached on the
    /// invoker and re-validated against the registry version — runtime registrations
    /// invalidate it exactly as they invalidate the executors' caches. Races are benign:
    /// concurrent writers publish equivalent, idempotent state.
    /// </summary>
    private IMessageDependencies? _cachedDependencies;
    private int _cachedVersion = int.MinValue;

    private IMessageDependencies GetPlan(
        MessageMediator mediator,
        ActualTypeOrFirstAssignableTypeMessageResolveStrategy resolveStrategy)
    {
        if (mediator.DependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            var cached = _cachedDependencies;

            if (cached is not null && _cachedVersion == typedFactory.CurrentRegistryVersion)
            {
                return cached;
            }

            var dependencies = BuildPlan(typedFactory, resolveStrategy);
            _cachedDependencies = dependencies;
            _cachedVersion = typedFactory.CurrentRegistryVersion;
            return dependencies;
        }

        return BuildPlan(mediator.DependenciesFactory, resolveStrategy);
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
        IExecutionContext context,
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
            ValueTask pending, TEvent @event, MessageDependencies plan, IExecutionContext context,
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
        Core.Abstractions.IHandlerReference<Core.Abstractions.Handlers.IHandler, Core.Abstractions.Registry.Descriptors.IMainHandlerDescriptor> reference,
        TEvent @event,
        IExecutionContext context,
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

    private static IMessageDependencies BuildPlan(
        Core.Abstractions.Factories.IMessageDependenciesFactory factory,
        ActualTypeOrFirstAssignableTypeMessageResolveStrategy resolveStrategy)
    {
        // Mirrors MessageMediator.Mediate's descriptor handling for the options the old
        // path used: RegisterPlainMessagesOnSpot was never set for events, so an
        // unregistered event type throws NoHandlerFoundException here as it did there.
        var descriptor = resolveStrategy.Find(typeof(TEvent))
                         ?? throw new NoHandlerFoundException(typeof(TEvent));

        return factory.Create(typeof(TEvent), descriptor, EmptyGroups);
    }
}

/// <summary>
/// Process-wide cache of <see cref="IEventBroadcastInvoker"/> instances, one per event
/// runtime type — one <see cref="Type.MakeGenericType"/> per event type.
/// </summary>
internal static class EventBroadcastInvokerCache
{
    private static readonly ConcurrentDictionary<Type, IEventBroadcastInvoker> Invokers = new();

    [UnconditionalSuppressMessage("Trimming", "IL2055",
        Justification = "The invoker generic is closed over a live event's runtime type; the event roots its type.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Generated dispatch roots cover source-generated event types; this reflective path is the " +
                        "JIT fallback for runtime-only registrations.")]
    public static IEventBroadcastInvoker Get(Type eventType)
    {
        if (Invokers.TryGetValue(eventType, out var invoker))
        {
            return invoker;
        }

        // Generated dispatch roots close the invoker generic at compile time; the
        // reflective path below only serves event types without a root.
        if (GeneratedDispatchRoots.FindMessage(eventType) is { } root)
        {
            return Invokers.GetOrAdd(eventType, root.Accept(InvokerVisitor.Instance, state: false));
        }

        return Invokers.GetOrAdd(eventType,
            static t => (IEventBroadcastInvoker)Activator.CreateInstance(typeof(EventBroadcastInvoker<>).MakeGenericType(t))!);
    }

    /// <summary>
    /// Re-enters a generic context with a root's event type and constructs the closed
    /// broadcast invoker there — no <see cref="Type.MakeGenericType"/>, no reflection.
    /// </summary>
    private sealed class InvokerVisitor : IMessageRootVisitor<IEventBroadcastInvoker, bool>
    {
        public static readonly InvokerVisitor Instance = new();

        public IEventBroadcastInvoker Visit<TMessage>(bool state) where TMessage : IMessage
            => new EventBroadcastInvoker<TMessage>();
    }
}
