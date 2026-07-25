using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Registry;

/// <summary>
/// Records the DI lifetime each handler/interceptor type was registered with, and answers
/// whether a message's complete pipeline is singleton-only (eligible for the process-wide
/// memoized fast path). Types with no known registration are treated as non-singleton,
/// which routes them to the always-correct per-scope resolution path.
/// </summary>
internal sealed class HandlerLifetimeRegistry(IReadOnlyDictionary<Type, ServiceLifetime> lifetimes)
{
    private readonly ConcurrentDictionary<Type, bool> _allSingletonByMessageType = new();
    private int _version = -1;

    /// <summary>
    /// Clears cached verdicts when the message registry version changes (runtime
    /// registrations can add handlers whose lifetime is unknown).
    /// </summary>
    public void InvalidateIfRegistryChanged(int registryVersion)
    {
        if (Volatile.Read(ref _version) == registryVersion)
        {
            return;
        }

        _allSingletonByMessageType.Clear();
        Volatile.Write(ref _version, registryVersion);
    }

    public bool AreAllHandlersSingleton(Type messageType, IMessageDescriptor descriptor)
    {
        if (_allSingletonByMessageType.TryGetValue(messageType, out var cached))
        {
            return cached;
        }

        var verdict = Compute(descriptor);
        _allSingletonByMessageType[messageType] = verdict;
        return verdict;
    }

    private bool Compute(IMessageDescriptor descriptor)
    {
        return AllSingleton(descriptor.Handlers)
               && AllSingleton(descriptor.IndirectHandlers)
               && AllSingleton(descriptor.PreInterceptors)
               && AllSingleton(descriptor.IndirectPreInterceptors)
               && AllSingleton(descriptor.PostInterceptors)
               && AllSingleton(descriptor.IndirectPostInterceptors)
               && AllSingleton(descriptor.ExceptionInterceptors)
               && AllSingleton(descriptor.IndirectExceptionInterceptors)
               && AllSingleton(descriptor.FinalInterceptors)
               && AllSingleton(descriptor.IndirectFinalInterceptors);
    }

    private bool AllSingleton<TDescriptor>(IReadOnlyCollection<TDescriptor> descriptors)
        where TDescriptor : IHandlerDescriptor
    {
        foreach (var descriptor in descriptors)
        {
            if (!lifetimes.TryGetValue(descriptor.HandlerType, out var lifetime) || lifetime != ServiceLifetime.Singleton)
            {
                return false;
            }
        }

        return true;
    }
}
