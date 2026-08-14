using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Internal.Mediator;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events;


/// <summary>
/// Mediates events through frozen publish pipelines closed over each event's runtime type,
/// so handlers are always invoked through their typed members — including for the
/// interface-erased <see cref="PublishAsync(IEvent, IEnumerable{string}, bool, CancellationToken)"/> overload.
/// </summary>
/// <remarks>
/// <para>
/// Unsealed so the DI registration can bind the engine-backed constructor through a
/// single-constructor derived shape; the facade carries no state a derived type could
/// corrupt.
/// </para>
/// <para>
/// Every publish entry is one body: guard the runtime type, read the frozen pipeline's
/// slot, execute pooled. The entries deliberately do not relay through each other — each
/// generic entry a caller's interface dispatch lands on carries the complete fast path,
/// because every relay frame between the facade and the pipeline is a measured per-publish
/// cost the pipeline itself never earns back.
/// </para>
/// </remarks>
/// <inheritdoc cref="IEventMediator"/>
public class EventMediator : IPublisher
{
    /// <summary>
    /// This container's frozen publish pipelines — the engine's table, captured once so a
    /// publish reads a field instead of chasing the engine.
    /// </summary>
    private readonly FrozenBroadcastTable _broadcasts;

    /// <summary>
    /// The scope provider handlers resolve against.
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Publishes go straight to the process-wide engine's broadcast table with
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

        _broadcasts = engine.Broadcasts;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public ValueTask PublishAsync(IEvent @event, IEnumerable<string>? groups, bool throwIfNoHandlerFound,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return _broadcasts.Get(@event.GetType())
            .PublishPooled(@event, _serviceProvider, cancellationToken, groups, throwIfNoHandlerFound);
    }

    /// <inheritdoc />
    public ValueTask PublishAsync<TEvent>(TEvent @event, IEnumerable<string>? groups, bool throwIfNoHandlerFound,
        CancellationToken cancellationToken)
        where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(@event);

        // When the runtime type is exactly TEvent (the overwhelmingly common typed publish)
        // the pipeline comes from a static-generic slot instead of the type-keyed
        // dictionary. A base-typed generic call keeps resolving by the runtime type — the
        // slot for a base TEvent would dispatch the wrong closed pipeline.
        var dispatch = @event.GetType() == typeof(TEvent)
            ? _broadcasts.Get<TEvent>()
            : _broadcasts.Get(@event.GetType());

        return dispatch.PublishPooled(@event, _serviceProvider, cancellationToken, groups, throwIfNoHandlerFound);
    }

    /// <inheritdoc />
    public ValueTask PublishAsync(IEvent @event, ErgosfareContext context, IEnumerable<string>? groups = null,
        bool throwIfNoHandlerFound = false)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);

        return _broadcasts.Get(@event.GetType())
            .Publish(@event, context, _serviceProvider, groups, throwIfNoHandlerFound);
    }

    /// <summary>Publishes an event through its default pipeline.</summary>
    /// <remarks>
    /// The conveniences are declared here as well as on the interface. They used to be
    /// extension methods, which a concrete-typed receiver finds; a default interface method is
    /// not, so carrying them only on the interface would have broken every call made through
    /// this class. Each carries the full body — see the class remarks.
    /// </remarks>
    public ValueTask PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return _broadcasts.Get(@event.GetType())
            .PublishPooled(@event, _serviceProvider, cancellationToken, null, throwIfNoHandlerFound: false);
    }


    /// <summary>Publishes under a canonical group filter.</summary>
    public ValueTask PublishAsync(IEvent @event, GroupSet groups, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(groups);

        return _broadcasts.Get(@event.GetType()).PublishPooled(
            @event, _serviceProvider, cancellationToken,
            groups.Count == 0 ? null : groups, throwIfNoHandlerFound: false);
    }

    /// <summary>Publishes under a group filter given as a plain array.</summary>
    public ValueTask PublishAsync(IEvent @event, string[] groups, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return _broadcasts.Get(@event.GetType())
            .PublishPooled(@event, _serviceProvider, cancellationToken, groups, throwIfNoHandlerFound: false);
    }

    /// <summary>Typed counterpart of <see cref="PublishAsync(IEvent, CancellationToken)"/>.</summary>
    public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(@event);

        var dispatch = @event.GetType() == typeof(TEvent)
            ? _broadcasts.Get<TEvent>()
            : _broadcasts.Get(@event.GetType());

        return dispatch.PublishPooled(@event, _serviceProvider, cancellationToken, null, throwIfNoHandlerFound: false);
    }


    /// <summary>Typed counterpart of the canonical group-filter overload.</summary>
    public ValueTask PublishAsync<TEvent>(TEvent @event, GroupSet groups, CancellationToken cancellationToken = default)
        where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(groups);

        var dispatch = @event.GetType() == typeof(TEvent)
            ? _broadcasts.Get<TEvent>()
            : _broadcasts.Get(@event.GetType());

        return dispatch.PublishPooled(
            @event, _serviceProvider, cancellationToken,
            groups.Count == 0 ? null : groups, throwIfNoHandlerFound: false);
    }
}
