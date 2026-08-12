using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
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
    /// <summary>
    /// Publishes against the dispatch engine: the concrete machinery is known by
    /// construction, so publishing runs directly against the invoker-cached plan and the
    /// caller's scope provider — grouped filters included, resolved from the grouped plan
    /// slot. An externally owned context (nested publish) is used as-is.
    /// <paramref name="groupsOverride"/> carries a facade-level group filter (a
    /// <see cref="GroupSet"/>) without a settings object; when present it takes
    /// precedence over the settings' groups.
    /// </summary>
    ValueTask Publish(object @event, EventMediationSettings? settings, CancellationToken cancellationToken,
        MessageDispatchEngine engine, IServiceProvider serviceProvider,
        ErgosfareContext? externalContext = null,
        IEnumerable<string>? groupsOverride = null);
}
