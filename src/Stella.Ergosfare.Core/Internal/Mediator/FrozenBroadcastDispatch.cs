using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One container's frozen publish pipeline for one event type, carried erased so the table
/// can hold every type in one dictionary.
/// </summary>
/// <remarks>
/// Frozen is the contract: every decision a publish needs — plan or runtime body, bare loop
/// or staged pipeline, nothing at all — is made once, at construction, from the container's
/// settled composition. A dispatch reads no verdict, consults no gate, and materializes no
/// dependencies; it finds this object and executes what it holds. The runtime delivery
/// bodies live inside the typed closure as the plan family's N-handler base case — not as a
/// separate strategy lane a dispatch could fall into.
/// </remarks>
internal abstract class FrozenBroadcastDispatch
{
    /// <summary>
    /// Publishes under an externally owned context — the nested-publish path. The caller
    /// owns the context's lifetime, so nothing is rented and nothing is returned.
    /// </summary>
    internal abstract ValueTask Publish(
        object message,
        ErgosfareContext context,
        IServiceProvider serviceProvider,
        IEnumerable<string>? groups);

    /// <summary>
    /// The pooled publish in one frame — rent, execute, return — so the hot path carries no
    /// separate renting frame between the caller and the delivery.
    /// </summary>
    internal abstract ValueTask PublishPooled(
        object message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        IEnumerable<string>? groups);
}
