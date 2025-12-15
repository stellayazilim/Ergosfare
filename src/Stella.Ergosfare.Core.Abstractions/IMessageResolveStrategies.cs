using System;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Abstractions;

public interface IMessageResolveStrategy
{
    /// <summary>
    ///     Finds a message descriptor for the specified message type from the message registry.
    /// </summary>
    /// <param name="messageType">The type of the message to find a descriptor for.</param>
    /// <returns>
    ///     The message descriptor if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    ///     The implementation determines the specific rules for matching a message type to a descriptor.
    ///     For example, it might look for an exact type match, or it might consider inheritance relationships.
    /// </remarks>
    IMessageDescriptor? Find(Type messageType);
}