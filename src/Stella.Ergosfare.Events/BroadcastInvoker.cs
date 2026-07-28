using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Contexts;
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

        // Root publish: rent a pooled context and hand it to Mediate as an external
        // context. This invoker is the completion observer — the broadcast strategy is a
        // plain async ValueTask that awaits every handler and interceptor sequentially, so
        // the returned task's completion really is the end of all context use.
        // Adopting the settings' items dictionary keeps handler writes visible to the
        // caller exactly as the unpooled path did; on return the context detaches the
        // dictionary instead of wiping it.
        var context = ErgosfareExecutionContextPool.Rent(settings?.Items, cancellationToken);

        var options = new MediateOptions<TEvent, ValueTask>
        {
            MessageMediationStrategy = strategy,
            MessageResolveStrategy = resolveStrategy,
            CancellationToken = cancellationToken,
            Groups = settings is null ? EmptyGroups : settings.Filters.Groups,
            ExternalContext = context,
        };

        ValueTask task;

        try
        {
            task = mediator.Mediate((TEvent)@event, options);
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
