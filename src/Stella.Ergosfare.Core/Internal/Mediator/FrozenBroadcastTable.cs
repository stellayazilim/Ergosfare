using System.Collections.Concurrent;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One container's publish pipelines, keyed by event type — the publishing counterpart of
/// <see cref="PipelineExecutorCache"/>.
/// </summary>
/// <param name="dependenciesFactory">The factory the pipelines resolve participants through.</param>
/// <remarks>
/// The table belongs to its container rather than the process, so containers need no
/// guarding against each other here.
/// </remarks>
internal sealed class FrozenBroadcastTable(IMessageDependenciesFactory dependenciesFactory)
{
    private readonly ConcurrentDictionary<Type, FrozenBroadcastDispatch> _byType = new();

    /// <summary>
    /// Returns the publish pipeline of an event known only by its runtime type.
    /// </summary>
    /// <param name="messageType">The event's runtime type.</param>
    /// <returns>The pipeline for that type.</returns>
    /// <remarks>
    /// A generated root closes the generic without reflection. A type the generator never
    /// saw gets the rootless pipeline instead: publishing it to nobody stays a no-op, and
    /// publishing it to somebody fails — nothing is dispatched at run time that was not
    /// produced at compile time.
    /// </remarks>
    internal FrozenBroadcastDispatch Get(Type messageType)
        => _byType.TryGetValue(messageType, out var dispatch)
            ? dispatch
            : _byType.GetOrAdd(
                messageType,
                GeneratedDispatchRoots.FindMessage(messageType) is { } root
                    ? root.Accept(DispatchVisitor.Instance, dependenciesFactory)
                    : new UnplannedBroadcastDispatch(dependenciesFactory, messageType));

    /// <summary>
    /// Returns the publish pipeline of an event type named at compile time.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <returns>The pipeline for that type.</returns>
    /// <remarks>
    /// A static generic slot turns the dictionary lookup into a field read plus a check that
    /// the slot belongs to this table, and the closed type is named outright rather than
    /// constructed. The check keeps containers apart; the dictionary behind it keeps pipeline
    /// identity stable across slot rewrites. Callers must first confirm
    /// <c>message.GetType() == typeof(TEvent)</c>, since publishing through a base type has
    /// to resolve by the runtime type.
    /// </remarks>
    internal FrozenBroadcastDispatch Get<TEvent>() where TEvent : notnull
    {
        var slot = Holder<TEvent>.Slot;

        if (slot is not null && ReferenceEquals(slot.Table, this))
        {
            return slot.Dispatch;
        }

        var dispatch = _byType.TryGetValue(typeof(TEvent), out var existing)
            ? existing
            : _byType.GetOrAdd(typeof(TEvent), new FrozenBroadcastDispatch<TEvent>(dependenciesFactory));

        Holder<TEvent>.Slot = new Slot(this, dispatch);
        return dispatch;
    }

    /// <summary>
    /// A table and the pipeline it served.
    /// </summary>
    /// <param name="table">The table the pipeline belongs to.</param>
    /// <param name="dispatch">The pipeline.</param>
    private sealed class Slot(FrozenBroadcastTable table, FrozenBroadcastDispatch dispatch)
    {
        public readonly FrozenBroadcastTable Table = table;
        public readonly FrozenBroadcastDispatch Dispatch = dispatch;
    }

    /// <summary>
    /// The last table to serve a compile-time lookup for one event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type this slot belongs to.</typeparam>
    // The type parameter is the key: one static slot per closed event type.
    // ReSharper disable once UnusedTypeParameter
    private static class Holder<TEvent> where TEvent : notnull
    {
        // ReSharper disable once StaticMemberInGenericType
        public static Slot? Slot;
    }

    /// <summary>
    /// Constructs a publish pipeline inside a generic context carrying the root's event
    /// type, so nothing is built reflectively.
    /// </summary>
    private sealed class DispatchVisitor : IMessageRootVisitor<FrozenBroadcastDispatch, IMessageDependenciesFactory>
    {
        /// <summary>
        /// The shared instance; the visitor holds no state.
        /// </summary>
        public static readonly DispatchVisitor Instance = new();

        /// <inheritdoc />
        public FrozenBroadcastDispatch Visit<TMessage>(IMessageDependenciesFactory state) where TMessage : IMessage
            => new FrozenBroadcastDispatch<TMessage>(state);
    }
}
