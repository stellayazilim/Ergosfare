using Stella.Ergosfare.Core.Abstractions.Planning;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Selects which of the compiled event constructs this container runs.
/// </summary>
/// <param name="compositions">
/// The catalog this builder records the container's selection in.
/// </param>
/// <exception cref="ArgumentNullException"><paramref name="compositions"/> is <c>null</c>.</exception>
public class EventModuleBuilder(DispatchPlanCatalog compositions)
{
    private readonly DispatchPlanCatalog _compositions = compositions ?? throw new ArgumentNullException(nameof(compositions));

    /// <summary>Applies the compile-time default selection for this module.</summary>
    public EventModuleBuilder AddGenerated() => AddGenerated("");

    /// <summary>Applies the compile-time selection for a constant discovery-key pattern.</summary>
    /// <param name="discoveryKeyPattern">An exact key or trailing-star prefix.</param>
    /// <returns>The same builder.</returns>
    public EventModuleBuilder AddGenerated(string discoveryKeyPattern)
    {
        GeneratedPlanRegistry.ApplyGeneratedSelection(_compositions, 3, discoveryKeyPattern);
        return this;
    }

    /// <summary>
    /// Registers one event construct.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The type to register: an event — which may be any non-null type — or one of the
    /// handlers and interceptors that serve events.
    /// </typeparam>
    /// <returns>The same builder, so calls can be chained.</returns>
    public EventModuleBuilder Register<TEvent>() where TEvent : notnull
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
    /// <remarks>The generator validates module membership at the selection call.</remarks>
    public EventModuleBuilder Register(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        _compositions.Select(eventType);
        return this;
    }

    /// <summary>
    /// Registers many participants at once — the path generated registration uses.
    /// </summary>
    /// <param name="participantTypes">The handler and interceptor types to register.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// The generator has already classified the selected types by module.
    /// </remarks>
    public EventModuleBuilder RegisterParticipants(IEnumerable<Type> participantTypes)
    {
        _compositions.Select(participantTypes);
        return this;
    }
}
