using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Abstractions.Registry;


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
    void Register([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type type);

    /// <summary>
    /// Registers pre-built handler descriptors, bypassing reflection-based descriptor
    /// construction entirely.
    /// </summary>
    /// <param name="descriptors">The handler descriptors to register.</param>
    /// <remarks>
    /// This is the injection seam for ahead-of-time registration (e.g. source-generated
    /// code): the caller supplies complete <see cref="IHandlerDescriptor"/> instances and the
    /// registry only links them to their message types. <see cref="Register"/> remains the
    /// reflection-based fallback; the two may be mixed — a handler type registered through
    /// either path is skipped by the other.
    /// </remarks>
    void RegisterDescriptors(IEnumerable<IHandlerDescriptor> descriptors);
}