using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Factories;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal.Mediator;

namespace Ergosfare.Core.Internal.Factories;

/// <summary>
/// Factory for creating <see cref="IMessageDependencies"/> instances.
/// </summary>
/// <param name="serviceProvider">The service provider used to resolve handlers and interceptors.</param>
public sealed class MessageDependenciesFactory(IServiceProvider serviceProvider): IMessageDependenciesFactory
{
    /// <summary>
    /// Creates a new <see cref="IMessageDependencies"/> for the specified message type and descriptor.
    /// </summary>
    /// <param name="messageType">The type of the message.</param>
    /// <param name="descriptor">The message descriptor containing handlers and interceptors.</param>
    /// <param name="groups">The groups to filter handlers and interceptors.</param>
    /// <returns>A <see cref="IMessageDependencies"/> instance for the message.</returns>
    public IMessageDependencies Create(Type messageType, IMessageDescriptor descriptor, IEnumerable<string> groups)
    {
        return new MessageDependencies(messageType, descriptor, serviceProvider, groups);
    }
}