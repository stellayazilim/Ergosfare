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
    /// <remarks>
    /// <c>notnull</c>, not <see cref="IEvent"/>. The whole publish lane is already declared
    /// over <c>notnull</c> — <c>IEventHandler&lt;TEvent&gt;</c>, <c>PublishAsync&lt;TEvent&gt;</c>,
    /// <c>FrozenBroadcastDispatch&lt;TEvent&gt;</c> — because a broadcast carries no result
    /// and needs nothing from <c>IMessage</c>. Requiring the marker here was the one place
    /// that forced an Ergosfare reference into the layer declaring a domain event, which is
    /// the wrong direction for a dependency to run.
    /// </remarks>
    /// <typeparam name="TEvent">The type to register.</typeparam>
    /// <returns>The current <see cref="EventModuleBuilder"/> instance for fluent chaining.</returns>
    public EventModuleBuilder Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TEvent>() where TEvent : notnull
    {
        Register(typeof(TEvent));
        return this;
    }

    /// <summary>
    /// Registers an event construct — an event, or one of the subscribers and interceptors
    /// serving events (their contracts carry the module marker too).
    /// </summary>
    /// <remarks>
    /// The module assertion applies to participants, not to messages. A subscriber or
    /// interceptor belongs to a module and registering one in the wrong module is a mistake
    /// worth reporting; a message does not belong to anything — a plain domain type with an
    /// <c>IEventHandler&lt;T&gt;</c> written for it is exactly the shape this lane exists to
    /// carry, and it has no marker to assert against. Selecting a type nothing subscribes to
    /// is inert rather than wrong: the catalog only ever asks whether a <em>participant</em>
    /// was selected.
    /// </remarks>
    /// <param name="eventType">The type to register.</param>
    /// <returns>The current <see cref="EventModuleBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="NotSupportedException">
    /// The type carries pipeline contracts but is not an event construct.
    /// </exception>
    public EventModuleBuilder Register([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        if (CarriesPipelineContracts(eventType) && !eventType.IsAssignableTo(typeof(IEvent)))
        {
            throw new NotSupportedException(
                $"The given type '{eventType.Name}' carries pipeline contracts but is not an event construct, so it cannot be registered here. "
                + "A message needs no marker; a participant belongs to its module.");
        }

        _compositions.Select(eventType);
        return this;
    }

    /// <summary>
    /// Whether the type implements any of the core pipeline contracts — which is what makes
    /// it a participant rather than a message.
    /// </summary>
    private static bool CarriesPipelineContracts(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
    {
        foreach (var contract in type.GetInterfaces())
        {
            if (contract.Namespace == CoreHandlerNamespace)
            {
                return true;
            }
        }

        return false;
    }

    private const string CoreHandlerNamespace = "Stella.Ergosfare.Core.Abstractions.Handlers";

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
