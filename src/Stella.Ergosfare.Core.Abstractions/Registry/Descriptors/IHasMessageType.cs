using System;

namespace Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;


/// <summary>
/// Represents an object that is associated with a specific message type.
/// </summary>
/// <remarks>
/// This interface is typically implemented by handler descriptors or other
/// components that operate on a specific message type. It exposes the
/// <see cref="MessageType"/> property so that the message type can be
/// inspected at runtime.
/// </remarks>
public interface IHasMessageType
{
    /// <summary>
    /// Gets the <see cref="Type"/> of the message associated with this object.
    /// </summary>
    Type MessageType { get; }
}