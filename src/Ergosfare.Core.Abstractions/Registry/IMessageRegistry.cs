using System;
using System.Collections.Generic;
using Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Ergosfare.Core.Abstractions.Registry;


/// <summary>
/// Provides a registry of message descriptors.
/// </summary>
/// <remarks>
/// The registry maintains a collection of <see cref="IMessageDescriptor"/> instances,
/// each describing the handlers and interceptors for a specific message type.
/// It allows for registering new message types and enumerating existing descriptors.
/// </remarks>
public interface IMessageRegistry : IReadOnlyCollection<IMessageDescriptor>
{
    /// <summary>
    /// Registers a new message type in the registry.
    /// </summary>
    /// <param name="type">The message type to register.</param>
    /// <remarks>
    /// If the type is already registered, this method may update or ignore it depending on the implementation.
    /// </remarks>
    void Register(Type type);
}