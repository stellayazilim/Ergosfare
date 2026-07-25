using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;


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
    public EventModuleBuilder Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TEvent>() where TEvent : IEvent
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
    public EventModuleBuilder Register([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type eventType)
    {
        if (!eventType.IsAssignableTo(typeof(IEvent)))
            throw new NotSupportedException($"The given type '{eventType.Name}' is not an event and cannot be registered.");
        
        messageRegistry.Register(eventType);
        return this;
    }

    /// <summary>
    /// Registers pre-built handler descriptors, bypassing reflection-based descriptor
    /// construction — the registration path used by source-generated code.
    /// </summary>
    /// <param name="descriptors">The descriptors to register; every handler type must be an event construct.</param>
    /// <returns>The current <see cref="EventModuleBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="NotSupportedException">Thrown when a descriptor's handler type is not an event construct.</exception>
    public EventModuleBuilder RegisterDescriptors(IEnumerable<IHandlerDescriptor> descriptors)
    {
        var accepted = new List<IHandlerDescriptor>();

        foreach (var descriptor in descriptors)
        {
            if (!descriptor.HandlerType.IsAssignableTo(typeof(IEvent)))
            {
                throw new NotSupportedException($"The given type '{descriptor.HandlerType.Name}' is not an event and cannot be registered.");
            }

            accepted.Add(descriptor);
        }

        messageRegistry.RegisterDescriptors(accepted);
        return this;
    }

    /// <summary>
    /// Registers the assembly's event types that participate in default discovery: types
    /// excluded via <see cref="ExcludeFromDiscoveryAttribute"/> or gated behind a
    /// <see cref="DiscoveryKeyAttribute"/> are skipped, mirroring source-generated
    /// <c>RegisterGenerated()</c>.
    /// </summary>
    /// <param name="assembly">The assembly to scan for types implementing <see cref="IEvent"/>.</param>
    /// <returns>The current <see cref="EventModuleBuilder"/> instance for fluent chaining.</returns>
    [RequiresUnreferencedCode("Assembly scanning discovers event types via reflection; trimming may remove them. Register events explicitly (or use source-generated registration) in trimmed or AOT applications.")]
    public EventModuleBuilder RegisterFromAssembly(Assembly assembly)
        => RegisterFromAssembly(assembly, DiscoveryKeyAttribute.DefaultKey);

    /// <summary>
    /// Registers the assembly's event types whose discovery keys match the given pattern —
    /// an exact key or a trailing-<c>*</c> prefix glob. See
    /// <see cref="DiscoveryKeyAttribute"/> for the key model.
    /// </summary>
    /// <param name="assembly">The assembly to scan for types implementing <see cref="IEvent"/>.</param>
    /// <param name="discoveryKeyPattern">The discovery key pattern to select types by.</param>
    /// <returns>The current <see cref="EventModuleBuilder"/> instance for fluent chaining.</returns>
    [RequiresUnreferencedCode("Assembly scanning discovers event types via reflection; trimming may remove them. Register events explicitly (or use source-generated registration) in trimmed or AOT applications.")]
    public EventModuleBuilder RegisterFromAssembly(Assembly assembly, string discoveryKeyPattern)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAssignableTo(typeof(IEvent)) && Discovery.Matches(type, discoveryKeyPattern))
            {
                messageRegistry.Register(type);
            }
        }

        return this;
    }
}