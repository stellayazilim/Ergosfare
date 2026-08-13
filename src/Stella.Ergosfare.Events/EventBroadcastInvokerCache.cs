using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;

namespace Stella.Ergosfare.Events;

/// <summary>
/// Process-wide cache of <see cref="IEventBroadcastInvoker"/> instances, one per event
/// runtime type — one <see cref="Type.MakeGenericType"/> per event type.
/// </summary>
internal static class EventBroadcastInvokerCache
{
    private static readonly ConcurrentDictionary<Type, IEventBroadcastInvoker> Invokers = new();

    /// <summary>
    /// Static-generic view of the cache for callers that know the event's concrete type at
    /// compile time: the invoker resolves once per closed type into a static readonly
    /// field, so the typed publish overload skips the per-call dictionary lookup. Shares
    /// the dictionary's instance, keeping the plan cache one-per-event-type (and
    /// factory-keyed, so container isolation is unchanged). Callers must guard with
    /// <c>@event.GetType() == typeof(TEvent)</c> — a base-typed generic call must keep
    /// resolving by the runtime type.
    /// </summary>
    internal static class Holder<TEvent> where TEvent : notnull
    {
        public static readonly IEventBroadcastInvoker Instance = Get(typeof(TEvent));
    }

    public static IEventBroadcastInvoker Get(Type eventType)
        => Invokers.TryGetValue(eventType, out var invoker)
            ? invoker
            : Invokers.GetOrAdd(
                eventType,
                DispatchLookup.OverMessage(
                    eventType, InvokerVisitor.Instance, state: false, typeof(EventBroadcastInvoker<>)));

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
