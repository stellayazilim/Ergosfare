using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;


/// <summary>
/// Provides a builder for selecting the event constructs this container runs from the
/// compiled composition table.
/// </summary>
/// <remarks>
/// Events and the subscribers serving them are registered individually or by
/// <see cref="Type"/>; what each event's pipeline looks like is decided at compile time.
/// </remarks>
/// <param name="compositions">
/// The container's composition catalog, told which constructs this registration selects.
/// </param>
public class EventModuleBuilder(FrozenCompositionCatalog compositions)
{
    private readonly FrozenCompositionCatalog _compositions = compositions ?? throw new ArgumentNullException(nameof(compositions));

    /// <summary>
    /// Registers an event construct.
    /// </summary>
    /// <typeparam name="TEvent">The type to register. Must be an event construct.</typeparam>
    /// <returns>The current <see cref="EventModuleBuilder"/> instance for fluent chaining.</returns>
    public EventModuleBuilder Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TEvent>() where TEvent : IEvent
    {
        Register(typeof(TEvent));
        return this;
    }

    /// <summary>
    /// Registers an event construct — an event, or one of the subscribers and interceptors
    /// serving events (their contracts carry the module marker too).
    /// </summary>
    /// <param name="eventType">The type to register.</param>
    /// <returns>The current <see cref="EventModuleBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="NotSupportedException" />
    /// Thrown when the provided type is not an event construct.
    public EventModuleBuilder Register([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type eventType)
    {
        if (!eventType.IsAssignableTo(typeof(IEvent)))
            throw new NotSupportedException($"The given type '{eventType.Name}' is not an event and cannot be registered.");

        _compositions.Select(eventType);
        return this;
    }

    /// <summary>
    /// Registers a batch of pipeline participants — the bulk path source-generated
    /// registration uses.
    /// </summary>
    /// <remarks>
    /// No module assertion here: the generator has already partitioned its discoveries by
    /// module, and not every participant contract carries the module marker (the modifying
    /// interceptor shapes are declared purely over the core contracts).
    /// <see cref="Register(Type)"/> keeps the assertion, since a hand-written registration
    /// is where a wrong-module type actually surfaces.
    /// </remarks>
    /// <param name="participantTypes">The subscriber and interceptor types to register.</param>
    /// <returns>The current <see cref="EventModuleBuilder"/> instance for fluent chaining.</returns>
    public EventModuleBuilder RegisterParticipants(IEnumerable<Type> participantTypes)
    {
        _compositions.Select(participantTypes);
        return this;
    }
}
