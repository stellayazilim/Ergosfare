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
