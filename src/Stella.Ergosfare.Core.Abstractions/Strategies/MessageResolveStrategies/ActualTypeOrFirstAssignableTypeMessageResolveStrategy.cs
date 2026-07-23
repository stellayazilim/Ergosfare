using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<Type, CacheEntry> _cache = new();

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

        // Registry Count is the exact-existence token: it changes precisely when a new
        // message type is registered. Exact-match entries stay valid forever (descriptors
        // are mutated in place when handlers are added, never replaced), while negative
        // and assignable-fallback entries are valid only for the registry size they were
        // computed against — an exact registration arriving later must win. Stamping each
        // entry instead of clearing the cache on growth also makes concurrent lookups
        // race-free: a lookup computed against a pre-registration snapshot self-invalidates
        // on the next read, whereas a cleared-then-reinserted stale entry never would.
        var registryCount = messageRegistry.Count;

        if (_cache.TryGetValue(messageType, out var entry)
            && (entry.IsExact || entry.RegistryCount == registryCount))
        {
            return entry.Descriptor;
        }

        var (descriptor, isExact) = FindUncached(messageType);
        _cache[messageType] = new CacheEntry(descriptor, registryCount, isExact);

        return descriptor;
    }

    private (IMessageDescriptor? Descriptor, bool IsExact) FindUncached(Type messageType)
    {
        IMessageDescriptor? firstAssignable = null;

        // Single pass: the registry guarantees message-type uniqueness, so the first exact
        // match is the only one; otherwise fall back to the first assignable descriptor.
        foreach (var descriptor in messageRegistry)
        {
            if (descriptor.MessageType == messageType)
            {
                return (descriptor, true);
            }

            if (firstAssignable is null && descriptor.MessageType.IsAssignableFrom(messageType))
            {
                firstAssignable = descriptor;
            }
        }

        return (firstAssignable, false);
    }

    /// <summary>
    /// A cached resolution: the descriptor (or <c>null</c> for a miss), the registry size
    /// it was computed against, and whether it was an exact message-type match.
    /// </summary>
    private readonly record struct CacheEntry(IMessageDescriptor? Descriptor, int RegistryCount, bool IsExact);
}
