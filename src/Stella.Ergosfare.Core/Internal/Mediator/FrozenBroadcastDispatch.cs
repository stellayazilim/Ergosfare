using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One container's publish pipeline for one event type, held without its type argument so
/// one table can hold every type.
/// </summary>
/// <remarks>
/// Everything a publish needs is decided at construction, from the participants the
/// container settled on: whether to run a compiled plan or a general body, and which body.
/// A publish reads no verdict and resolves no participants — it finds this object and runs
/// what it holds. The general bodies live inside the closed subclass.
/// </remarks>
internal abstract class FrozenBroadcastDispatch
{
    /// <summary>
    /// Publishes under an execution context the caller owns — the shape a nested publish
    /// uses.
    /// </summary>
    /// <param name="message">The event to publish.</param>
    /// <param name="context">The caller's execution context.</param>
    /// <param name="serviceProvider">The provider handlers are resolved from.</param>
    /// <param name="groups">The groups to deliver to, or <c>null</c> for the default.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    /// <remarks>
    /// The caller keeps ownership of the context, so nothing here creates or releases one.
    /// </remarks>
    internal abstract ValueTask Publish(
        object message,
        ErgosfareContext context,
        IServiceProvider serviceProvider,
        IEnumerable<string>? groups);

    /// <summary>
    /// Publishes under a context of its own, creating it and releasing it in the same frame.
    /// </summary>
    /// <param name="message">The event to publish.</param>
    /// <param name="serviceProvider">The provider handlers are resolved from.</param>
    /// <param name="cancellationToken">Token for the delivery.</param>
    /// <param name="groups">The groups to deliver to, or <c>null</c> for the default.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    /// <remarks>
    /// Doing both here keeps a separate frame off the path between the caller and the
    /// delivery.
    /// </remarks>
    internal abstract ValueTask PublishPooled(
        object message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        IEnumerable<string>? groups);
}
