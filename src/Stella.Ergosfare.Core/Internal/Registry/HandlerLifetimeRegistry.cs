using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;

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

    /// <summary>
    /// Whether every participant of a pipeline shape is registered as a singleton — the
    /// memoized fast path's eligibility.
    /// </summary>
    /// <remarks>
    /// Deliberately uncached: the verdict is a property of the SHAPE, and one message type
    /// has one shape per group set — a per-type cache here once let an empty group-less
    /// shape (vacuously all-singleton) stamp the type as memoizable, silently promoting the
    /// grouped shape's transient handlers to de-facto singletons. The caller's own
    /// per-(type, groups) graph cache already makes this a freeze-time-only computation.
    /// </remarks>
    public bool AreAllParticipantsSingleton(Type messageType, FrozenPipelineShape shape)
        => AllSingleton(shape.Handlers)
           && AllSingleton(shape.IndirectHandlers)
           && AllSingleton(shape.PreInterceptors)
           && AllSingleton(shape.PostInterceptors)
           && AllSingleton(shape.ExceptionInterceptors)
           && AllSingleton(shape.FinalInterceptors);

    private bool AllSingleton(IReadOnlyList<Type> participants)
    {
        for (var i = 0; i < participants.Count; i++)
        {
            if (!lifetimes.TryGetValue(participants[i], out var lifetime) || lifetime != ServiceLifetime.Singleton)
            {
                return false;
            }
        }

        return true;
    }
}
