using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// The publish pipeline of an event type with no generated dispatch root: a publish that
/// reaches nobody completes silently, and one that would reach somebody fails.
/// </summary>
/// <param name="dependenciesFactory">The factory subscribers are looked up through.</param>
/// <param name="eventType">The event type this dispatch answers for.</param>
/// <remarks>
/// Publishing an event type nobody serves is not an error — fire-and-forget keeps its
/// meaning, and the engine's own no-handler policy still applies above this. What cannot
/// happen is delivery: a subscriber only reachable through a pipeline the generator never
/// compiled would run code no plan produced.
/// </remarks>
internal sealed class UnplannedBroadcastDispatch(
    IMessageDependenciesFactory dependenciesFactory,
    Type eventType) : FrozenBroadcastDispatch
{
    /// <inheritdoc />
    internal override ValueTask Publish(
        object message,
        ErgosfareContext context,
        IServiceProvider serviceProvider,
        IEnumerable<string>? groups)
        => PublishCore(groups);

    /// <inheritdoc />
    internal override ValueTask PublishPooled(
        object message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        IEnumerable<string>? groups)
        => PublishCore(groups);

    /// <summary>
    /// Completes the publish when it reaches nobody and fails it when it would reach
    /// somebody.
    /// </summary>
    /// <param name="groups">The groups the publish asked for.</param>
    /// <returns>A completed task, when the publish reaches nobody.</returns>
    private ValueTask PublishCore(IEnumerable<string>? groups)
    {
        var dependencies = dependenciesFactory.Find(
            eventType, groups is null ? [] : [.. groups]);

        if (dependencies is null
            || (dependencies.Handlers.Count == 0 && dependencies.IndirectHandlers.Count == 0))
        {
            // Reaching nobody is not an error at run time — with no composition at all, or
            // with one that carries no handler. The engine's ThrowIfNoHandlerFound policy
            // is applied by the caller, not here.
            return default;
        }

        throw UnplannedDispatch.ForMissingDispatchRoot(eventType);
    }
}
