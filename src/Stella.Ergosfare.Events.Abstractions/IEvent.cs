using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Events.Abstractions;


/// <summary>
/// Represents a message or handler that is recognized by the event module.
/// </summary>
/// <remarks>
/// <para>
/// Any message type that implements <see cref="IEvent"/> is automatically treated as an event
/// by the messaging infrastructure.
/// </para>
/// <para>
/// Similarly, event handlers must implement or be decorated with <see cref="IEvent"/> 
/// to be registered within the event module.
/// </para>
/// <para>
/// The event module also supports simple POCO objects (plain old CLR objects),
/// allowing them to be registered and used as events.
/// </para>
/// <para>
/// Events are typically used in pub/sub scenarios, broadcasting information
/// to multiple handlers without expecting a return value.
/// </para>
/// <para>
/// Implementing <see cref="IEvent"/> indicates that a message or handler is 
/// registrable and discoverable within the event module for event dispatching.
/// </para>
/// </remarks>
public interface IEvent: IMessage;