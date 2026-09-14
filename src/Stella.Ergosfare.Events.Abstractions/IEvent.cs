using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Events.Abstractions;


/// <summary>
/// Marks a type as belonging to the event module — either an event that can be published,
/// or a participant in an event pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Events are published rather than sent: every registered handler receives one, and the
/// publisher gets nothing back. Handlers and interceptors carry this interface too, which
/// is how registration recognizes them as part of the module.
/// </para>
/// <para>
/// A plain object can be an event without implementing anything: the handler contracts
/// accept any non-null type, so the interface is not a requirement for the message itself.
/// </para>
/// </remarks>
public interface IEvent: IMessage;
