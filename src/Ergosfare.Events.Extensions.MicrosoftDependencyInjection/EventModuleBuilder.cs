using System.Reflection;
using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Events.Abstractions;

namespace Ergosfare.Events.Extensions.MicrosoftDependencyInjection;


/// <summary>
/// Provides a builder for registering events and their types within the event module.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="EventModuleBuilder"/> allows registering events individually,
/// by <see cref="Type"/>, or by scanning assemblies for all types implementing <see cref="IEvent"/>.
/// </para>
/// <para>
/// This builder interacts with the <see cref="IMessageRegistry"/> to register event types
/// so they can participate in the event mediation pipeline.
/// </para>
/// </remarks>
public class EventModuleBuilder(
    IMessageRegistry messageRegistry)
{

    /// <summary>
    /// Registers a generic event type <typeparamref name="TEvent"/> in the message registry.
    /// </summary>
    /// <typeparam name="TEvent">The event type to register. Must implement <see cref="IEvent"/>.</typeparam>
    /// <returns>The current <see cref="EventModuleBuilder"/> instance for fluent chaining.</returns>
    public EventModuleBuilder Register<TEvent>() where TEvent : IEvent
    {
        Register(typeof(TEvent));
        return this;
    }

    /// <summary>
    /// Registers an event type in the message registry.
    /// </summary>
    /// <param name="eventType">The event type to register. Must implement <see cref="IEvent"/>.</param>
    /// <returns>The current <see cref="EventModuleBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="NotSupportedException" />
    /// Thrown when the provided type does not implement <see cref="IEvent"/>.
    public EventModuleBuilder Register(Type eventType)
    {
        if (!eventType.IsAssignableTo(typeof(IEvent)))
            throw new NotSupportedException($"The given type '{eventType.Name}' is not an event and cannot be registered.");
        
        messageRegistry.Register(eventType);
        return this;
    }

    /// <summary>
    /// Registers all event types from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for types implementing <see cref="IEvent"/>.</param>
    /// <returns>The current <see cref="EventModuleBuilder"/> instance for fluent chaining.</returns>
    public EventModuleBuilder RegisterFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes()
                     .Where( t => t.IsAssignableTo(typeof(IEvent))))
        {
            messageRegistry.Register(type);
        }
        return this;
    }
    
}