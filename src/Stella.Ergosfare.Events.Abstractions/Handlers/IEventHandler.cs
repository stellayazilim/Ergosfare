using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Events.Abstractions;


/// <summary>
/// Handles events of type <typeparamref name="TEvent"/>.
/// </summary>
/// <typeparam name="TEvent">
/// The event type this handler accepts. Any non-null type will do — an event need not
/// implement <see cref="IEvent"/>.
/// </typeparam>
/// <remarks>
/// Unlike a command, an event may have any number of handlers, and every one of them
/// receives it. Handlers run one after another in the order the pipeline settled on, and
/// none of them returns anything to the publisher.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface IEventHandler<in TEvent>: IEvent, IAsyncHandler<TEvent> where TEvent : notnull;
