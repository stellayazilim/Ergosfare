
namespace Stella.Ergosfare.Core.Abstractions;


/// <summary>
/// Marks a type as a message that can be dispatched through an Ergosfare pipeline.
/// </summary>
/// <remarks>
/// The interface declares no members. It exists so handler and interceptor contracts can
/// constrain their message type argument, and so registration can recognize which types
/// participate in dispatch.
/// </remarks>
public interface IMessage;
