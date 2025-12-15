using System;
using System.Collections.Generic;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Abstractions.Factories;

/// <summary>
/// Factory interface for creating <see cref="IMessageDependencies"/> instances.
/// </summary>
public interface IMessageDependenciesFactory
{
    /// <summary>
    /// Creates a <see cref="IMessageDependencies"/> for the given message type and descriptor.
    /// </summary>
    /// <param name="messageType">The type of the message.</param>
    /// <param name="descriptor">The message descriptor containing handler and interceptor information.</param>
    /// <param name="groups">The groups to filter handlers by.</param>
    /// <returns>A <see cref="IMessageDependencies"/> instance for the specified message.</returns>
    public IMessageDependencies Create(Type messageType, IMessageDescriptor descriptor, IEnumerable<string> groups);
}