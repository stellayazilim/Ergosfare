using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
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
        IResultAdapterService? resultAdapterService, IExecutionContext? externalContext = null,
        IEnumerable<string>? groupsOverride = null);

    /// <summary>
    /// Engine-backed publish: the concrete dispatch machinery is known by construction, so
    /// the publish runs directly against the engine's plan and the caller's scope
    /// provider — grouped filters included, resolved from the grouped plan slot. External
    /// contexts resolve the scope's <see cref="IMessageMediator"/> on demand and run the
    /// original overload — semantics unchanged.
    /// <paramref name="groupsOverride"/> carries a facade-level group filter (a
    /// <see cref="GroupSet"/>) without a settings object; when present it takes
    /// precedence over the settings' groups.
    /// </summary>
    ValueTask Publish(object @event, EventMediationSettings? settings, CancellationToken cancellationToken,
        MessageDispatchEngine engine, IServiceProvider serviceProvider,
        ActualTypeOrFirstAssignableTypeMessageResolveStrategy resolveStrategy,
        IResultAdapterService? resultAdapterService, IExecutionContext? externalContext = null,
        IEnumerable<string>? groupsOverride = null);
}
