using System;
using System.Collections.Concurrent;
using System.Threading;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Abstractions.Strategies;


/// <summary>
///     Implements a message resolve strategy that first attempts to find a descriptor for the exact message type,
///     and if not found, returns the first descriptor for a type that is assignable from the message type.
/// </summary>
/// <remarks>
///     This strategy is useful for handling inheritance and interface implementation in the messaging system.
///     It allows messages to be handled by handlers registered for their exact type or for any base type or interface
///     that they implement. When multiple assignable types are found, the first one is returned.
///     Resolved descriptors are cached per message type; the cache is invalidated whenever the registry grows.
/// </remarks>
public sealed class ActualTypeOrFirstAssignableTypeMessageResolveStrategy(IMessageRegistry messageRegistry) : IMessageResolveStrategy
{
    private readonly ConcurrentDictionary<Type, IMessageDescriptor?> _cache = new();
    private int _cachedRegistryCount;

    /// <summary>
    ///     Finds a message descriptor for the specified message type from the message registry.
    /// </summary>
    /// <param name="messageType">The type of the message to find a descriptor for.</param>
    /// <returns>
    ///     The message descriptor for the exact message type if found; otherwise, the first descriptor
    ///     for a type that is assignable from the message type; or <c>null</c> if no suitable descriptor is found.
    /// </returns>
    /// <remarks>
    ///     For generic types, this method uses the generic type definition for matching.
    /// </remarks>
    public IMessageDescriptor? Find(Type messageType)
    {
        if (messageType.IsGenericType)
        {
            messageType = messageType.GetGenericTypeDefinition();
        }

        // The registry only ever grows; a change in Count means new message types were
        // registered and previously-missed lookups may now succeed. Descriptors themselves
        // are mutated in place when new handlers are added, so cached references stay valid.
        var registryCount = messageRegistry.Count;
        if (Volatile.Read(ref _cachedRegistryCount) != registryCount)
        {
            _cache.Clear();
            Volatile.Write(ref _cachedRegistryCount, registryCount);
        }

        if (_cache.TryGetValue(messageType, out var descriptor))
        {
            return descriptor;
        }

        return _cache.GetOrAdd(messageType, FindUncached);
    }

    private IMessageDescriptor? FindUncached(Type messageType)
    {
        IMessageDescriptor? firstAssignable = null;

        // Single pass: the registry guarantees message-type uniqueness, so the first exact
        // match is the only one; otherwise fall back to the first assignable descriptor.
        foreach (var descriptor in messageRegistry)
        {
            if (descriptor.MessageType == messageType)
            {
                return descriptor;
            }

            if (firstAssignable is null && descriptor.MessageType.IsAssignableFrom(messageType))
            {
                firstAssignable = descriptor;
            }
        }

        return firstAssignable;
    }
}
