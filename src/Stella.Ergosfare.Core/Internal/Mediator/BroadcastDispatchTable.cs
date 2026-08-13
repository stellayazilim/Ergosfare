using System.Collections.Concurrent;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One container's table of broadcast pipelines, keyed by message type — the publishing
/// counterpart of the executor cache, and the same shape: a table the container owns, not a
/// process-wide store every container has to be guarded against.
/// </summary>
internal sealed class BroadcastDispatchTable(IMessageDependenciesFactory dependenciesFactory)
{
    private readonly ConcurrentDictionary<Type, BroadcastDispatch> _byType = new();

    /// <summary>
    /// The pipeline for a message known only by its runtime type. The generated root closes
    /// the generic without reflection; a type the generator never saw falls to the reflective
    /// construction inside <see cref="DispatchLookup"/>.
    /// </summary>
    internal BroadcastDispatch Get(Type messageType)
        => _byType.TryGetValue(messageType, out var dispatch)
            ? dispatch
            : _byType.GetOrAdd(
                messageType,
                DispatchLookup.OverMessage(
                    messageType, DispatchVisitor.Instance, dependenciesFactory,
                    typeof(BroadcastDispatch<>), [dependenciesFactory]));

    /// <summary>
    /// The pipeline for a message whose type the caller knows at compile time: a
    /// static-generic slot replaces the dictionary lookup with a field read and a table
    /// identity check, and the closed type is named directly rather than constructed.
    /// </summary>
    /// <remarks>
    /// The slot is keyed by this table, so containers stay isolated — a foreign table's
    /// pipeline is never served, and the dictionary below keeps identity stable across slot
    /// refreshes. Callers must guard with <c>message.GetType() == typeof(TEvent)</c>; a
    /// base-typed generic call has to keep resolving by the runtime type.
    /// </remarks>
    internal BroadcastDispatch Get<TEvent>() where TEvent : notnull
    {
        var slot = Holder<TEvent>.Slot;

        if (slot is not null && ReferenceEquals(slot.Table, this))
        {
            return slot.Dispatch;
        }

        var dispatch = _byType.TryGetValue(typeof(TEvent), out var existing)
            ? existing
            : _byType.GetOrAdd(typeof(TEvent), new BroadcastDispatch<TEvent>(dependenciesFactory));

        Holder<TEvent>.Slot = new Slot(this, dispatch);
        return dispatch;
    }

    private sealed class Slot(BroadcastDispatchTable table, BroadcastDispatch dispatch)
    {
        public readonly BroadcastDispatchTable Table = table;
        public readonly BroadcastDispatch Dispatch = dispatch;
    }

    // The type parameter is the cache key: one static slot per closed message type.
    // ReSharper disable once UnusedTypeParameter
    private static class Holder<TEvent> where TEvent : notnull
    {
        // ReSharper disable once StaticMemberInGenericType
        public static Slot? Slot;
    }

    /// <summary>
    /// Re-enters a generic context with a root's message type and constructs the closed
    /// pipeline there — no <see cref="Type.MakeGenericType"/>, no reflection.
    /// </summary>
    private sealed class DispatchVisitor : IMessageRootVisitor<BroadcastDispatch, IMessageDependenciesFactory>
    {
        public static readonly DispatchVisitor Instance = new();

        public BroadcastDispatch Visit<TMessage>(IMessageDependenciesFactory state) where TMessage : IMessage
            => new BroadcastDispatch<TMessage>(state);
    }
}
