using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;


/// <summary>
/// Represents an asynchronous handler for a specific event type.
/// </summary>
/// <typeparam name="TEvent">The type of event to handle. Must implement <see cref="IEvent"/>.</typeparam>
/// <remarks>
/// <para>
/// Implementing <see cref="IEventHandler{TEvent}"/> allows the handler to be automatically
/// registered and invoked by the event mediation pipeline whenever an event of type <typeparamref name="TEvent"/> is published.
/// </para>
/// <para>
/// Handlers should implement the <see cref="IAsyncHandler{TEvent}"/> interface to provide
/// asynchronous processing logic for the event.
/// </para>
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventHandler<in TEvent>: IEvent, IAsyncHandler<TEvent> where TEvent : notnull;