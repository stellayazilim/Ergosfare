using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Selects which of the compiled event constructs this container runs.
/// </summary>
/// <param name="compositions">
/// The catalog this builder records the container's selection in.
/// </param>
/// <exception cref="ArgumentNullException"><paramref name="compositions"/> is <c>null</c>.</exception>
public class EventModuleBuilder(FrozenCompositionCatalog compositions)
{
    private readonly FrozenCompositionCatalog _compositions = compositions ?? throw new ArgumentNullException(nameof(compositions));

    /// <summary>
    /// Registers one event construct.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The type to register: an event — which may be any non-null type — or one of the
    /// handlers and interceptors that serve events.
    /// </typeparam>
    /// <returns>The same builder, so calls can be chained.</returns>
    public EventModuleBuilder Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TEvent>() where TEvent : notnull
    {
        Register(typeof(TEvent));
        return this;
    }

    /// <summary>
    /// Registers one event construct.
    /// </summary>
    /// <param name="eventType">
    /// The type to register: an event, or one of the handlers and interceptors that serve
    /// events.
    /// </param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="eventType"/> is <c>null</c>.</exception>
    /// <exception cref="NotSupportedException">
    /// The type is a pipeline participant but belongs to another module.
    /// </exception>
    /// <remarks>
    /// An event itself needs no marker, so any type is accepted as one. A participant is
    /// held to its module: a type implementing a pipeline contract must also carry
    /// <see cref="IEvent"/> to be registered here.
    /// </remarks>
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
    /// Reports whether a type implements any pipeline contract, which is what makes it a
    /// participant rather than a message.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><c>true</c> when it implements a handler or interceptor contract.</returns>
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

    /// <summary>
    /// The namespace every pipeline contract lives in.
    /// </summary>
    private const string CoreHandlerNamespace = "Stella.Ergosfare.Core.Abstractions.Handlers";

    /// <summary>
    /// Registers many participants at once — the path generated registration uses.
    /// </summary>
    /// <param name="participantTypes">The handler and interceptor types to register.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// Unlike <see cref="Register(Type)"/> this does not check the module: the generator has
    /// already sorted its discoveries by module.
    /// </remarks>
    public EventModuleBuilder RegisterParticipants(IEnumerable<Type> participantTypes)
    {
        _compositions.Select(participantTypes);
        return this;
    }
}
