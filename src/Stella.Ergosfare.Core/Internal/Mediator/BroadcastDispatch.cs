using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One container's broadcast pipeline for one message type, carried erased so the table can
/// hold every type in one dictionary.
/// </summary>
/// <remarks>
/// This belongs to a container rather than to the process. The broadcast lane used to be the
/// other way round — one object per event type, process-wide, carrying the last serving
/// container's factory as part of its cache key so a second container could not be served
/// stale state. Owning it per container says the same thing structurally and costs no guard.
/// </remarks>
internal abstract class BroadcastDispatch
{
    /// <summary>
    /// Delivers the message to its pipeline: the compiled plan when the live composition is
    /// the one it was baked against, the runtime delivery otherwise.
    /// </summary>
    internal abstract ValueTask Publish(
        object message,
        ErgosfareContext context,
        IServiceProvider serviceProvider,
        IEnumerable<string>? groups,
        bool throwIfNoHandlerFound);
}
