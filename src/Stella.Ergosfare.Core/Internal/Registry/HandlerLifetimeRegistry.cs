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
/// <param name="lifetimes">Effective DI lifetime per handler type, captured at module initialization.</param>
/// <param name="plainTransientRegistrations">
/// Handler types whose effective registration is a plain transient self-registration
/// (implementation type equals the service type, no factory, no instance, unkeyed) — the
/// exact shape the module's own <c>TryAddTransient</c> produces. For these, container
/// resolution and direct construction are semantically identical, which is what licenses
/// a generated plan's <c>new()</c> fast path. Types not in the set — user factories,
/// lifetime overrides, runtime-only registrations — always resolve through the container.
/// </param>
internal sealed class HandlerLifetimeRegistry(
    IReadOnlyDictionary<Type, ServiceLifetime> lifetimes,
    IReadOnlyCollection<Type>? plainTransientRegistrations = null)
{
    private readonly HashSet<Type> _plainTransients =
        plainTransientRegistrations as HashSet<Type> ?? [.. plainTransientRegistrations ?? []];

    /// <summary>
    /// Whether the handler type's effective registration is the module's own plain
    /// transient shape; see the constructor documentation. Captured at initialization —
    /// the same capture window the lifetime dictionary (and with it the memoized fast
    /// path) already relies on.
    /// </summary>
    public bool IsPlainTransientRegistration(Type handlerType) => _plainTransients.Contains(handlerType);

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
