using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events;


/// <summary>
/// Mediates events through broadcast pipelines closed over each event's runtime type, so
/// handlers are always invoked through their typed members — including for the
/// interface-erased <see cref="PublishAsync(IEvent, EventMediationSettings?, CancellationToken)"/> overload.
/// </summary>
/// <remarks>
/// Unsealed so the DI registration can bind the engine-backed constructor through a
/// single-constructor derived shape; the facade carries no state a derived type could
/// corrupt.
/// </remarks>
/// <inheritdoc cref="IEventMediator"/>
public class EventMediator : IPublisher
{
    /// <summary>
    /// The singleton dispatch engine every publish runs against.
    /// </summary>
    private readonly MessageDispatchEngine _engine;

    /// <summary>
    /// The scope provider handlers resolve against.
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Publishes go straight to the process-wide engine's broadcast plan with
    /// <paramref name="serviceProvider"/> as the handler-resolution scope, making the
    /// facade the only object built per resolution.
    /// </summary>
    /// <param name="engine">The singleton dispatch engine.</param>
    /// <param name="serviceProvider">The provider of the scope this facade serves.</param>
    public EventMediator(
        MessageDispatchEngine engine,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _engine = engine;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public ValueTask PublishAsync(IEvent @event, IEnumerable<string>? groups, bool throwIfNoHandlerFound,
        CancellationToken cancellationToken)
        => _engine.BroadcastAsync(
            @event, _serviceProvider, cancellationToken, groups, throwIfNoHandlerFound);

    /// <inheritdoc />
    public ValueTask PublishAsync<TEvent>(TEvent @event, IEnumerable<string>? groups, bool throwIfNoHandlerFound,
        CancellationToken cancellationToken)
        where TEvent : notnull
        // The typed entry: when the runtime type is exactly TEvent (the overwhelmingly common
        // typed publish) the pipeline comes from a static-generic slot instead of the
        // type-keyed dictionary. The engine applies that guard itself.
        => _engine.BroadcastAsync<TEvent>(
            @event, _serviceProvider, cancellationToken, groups, throwIfNoHandlerFound);

    /// <inheritdoc />
    public ValueTask PublishAsync(IEvent @event, ErgosfareContext context, IEnumerable<string>? groups = null,
        bool throwIfNoHandlerFound = false)
        => _engine.BroadcastAsync(@event, context, _serviceProvider, groups, throwIfNoHandlerFound);

    /// <summary>Publishes an event through its default pipeline.</summary>
    /// <remarks>
    /// The conveniences are declared here as well as on the interface. They used to be
    /// extension methods, which a concrete-typed receiver finds; a default interface method is
    /// not, so carrying them only on the interface would have broken every call made through
    /// this class.
    /// </remarks>
    public ValueTask PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
        => PublishAsync(@event, (IEnumerable<string>?)null, false, cancellationToken);


    /// <summary>Publishes under a canonical group filter.</summary>
    public ValueTask PublishAsync(IEvent @event, GroupSet groups, CancellationToken cancellationToken = default)
        => PublishAsync(@event, groups.Count == 0 ? null : (IEnumerable<string>?)groups, false, cancellationToken);

    /// <summary>Publishes under a group filter given as a plain array.</summary>
    public ValueTask PublishAsync(IEvent @event, string[] groups, CancellationToken cancellationToken = default)
        => PublishAsync(@event, (IEnumerable<string>?)groups, false, cancellationToken);

    /// <summary>Typed counterpart of <see cref="PublishAsync(IEvent, CancellationToken)"/>.</summary>
    public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull
        => PublishAsync(@event, (IEnumerable<string>?)null, false, cancellationToken);


    /// <summary>Typed counterpart of the canonical group-filter overload.</summary>
    public ValueTask PublishAsync<TEvent>(TEvent @event, GroupSet groups, CancellationToken cancellationToken = default)
        where TEvent : notnull
        => PublishAsync(@event, groups.Count == 0 ? null : (IEnumerable<string>?)groups, false, cancellationToken);
}
