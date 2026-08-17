using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;

namespace Stella.Ergosfare.Core.Internal.Registry;

/// <summary>
/// Remembers how each participant type was registered, and answers the two questions the
/// dispatch machinery asks about registrations.
/// </summary>
/// <param name="lifetimes">
/// The lifetime each participant type was registered with, captured during module
/// initialization.
/// </param>
/// <param name="plainTransientRegistrations">
/// The participant types registered in the module's own plain transient shape: registered
/// as themselves, unkeyed, with no factory and no instance. Constructing such a type and
/// resolving it come to the same thing, which is what lets a generated plan use <c>new</c>.
/// Anything else — a user's factory, an overridden lifetime, a registration made at runtime
/// — always goes through the container.
/// </param>
/// <remarks>
/// A type with no recorded registration counts as non-singleton, which sends it down the
/// per-scope path that is correct either way.
/// </remarks>
internal sealed class HandlerLifetimeRegistry(
    IReadOnlyDictionary<Type, ServiceLifetime> lifetimes,
    IReadOnlyCollection<Type>? plainTransientRegistrations = null)
{
    private readonly HashSet<Type> _plainTransients =
        plainTransientRegistrations as HashSet<Type> ?? [.. plainTransientRegistrations ?? []];

    /// <summary>
    /// Reports whether <paramref name="handlerType"/> was registered in the plain transient
    /// shape described on the constructor.
    /// </summary>
    /// <param name="handlerType">The participant type to ask about.</param>
    /// <returns><c>true</c> when constructing it directly is equivalent to resolving it.</returns>
    public bool IsPlainTransientRegistration(Type handlerType) => _plainTransients.Contains(handlerType);

    /// <summary>
    /// Reports whether every participant of <paramref name="shape"/> was registered as a
    /// singleton, which is what makes the pipeline eligible to resolve once and keep its
    /// instances.
    /// </summary>
    /// <param name="messageType">The message type the pipeline belongs to.</param>
    /// <param name="shape">The pipeline to examine.</param>
    /// <returns><c>true</c> when every participant is a singleton.</returns>
    /// <remarks>
    /// Answered fresh every time, on purpose. The answer belongs to the pipeline, not the
    /// message type, and one message type has a different pipeline per group set: caching
    /// it per type once let an empty ungrouped pipeline — singleton-only because it is
    /// empty — mark the type as memoizable, turning the grouped pipeline's transient
    /// participants into singletons in practice. The caller caches per (type, groups)
    /// anyway, so this runs only while a pipeline is being built.
    /// </remarks>
    public bool AreAllParticipantsSingleton(Type messageType, FrozenPipelineShape shape)
        => AllSingleton(shape.Handlers)
           && AllSingleton(shape.IndirectHandlers)
           && AllSingleton(shape.PreInterceptors)
           && AllSingleton(shape.PostInterceptors)
           && AllSingleton(shape.ExceptionInterceptors)
           && AllSingleton(shape.FinalInterceptors);

    /// <summary>
    /// Reports whether every type in one stage was registered as a singleton.
    /// </summary>
    /// <param name="participants">The stage's participant types.</param>
    /// <returns>
    /// <c>true</c> when all of them are singletons; an unregistered type counts as not.
    /// </returns>
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
