using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;


/// <summary>
///     Builder class for registering command types in the message registry.
/// </summary>
public sealed class CommandModuleBuilder
{
    private readonly IMessageRegistry _messageRegistry;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CommandModuleBuilder" /> class.
    /// </summary>
    /// <param name="messageRegistry">The message registry to which commands will be registered.</param>
    public CommandModuleBuilder(IMessageRegistry messageRegistry)
    {
        _messageRegistry = messageRegistry;
    }

    /// <summary>
    ///     Registers a command type for the message registry.
    /// </summary>
    /// <typeparam name="T">The type of command to register, which must implement <see cref="IRegistrableCommandConstruct" />.</typeparam>
    /// <returns>The current <see cref="CommandModuleBuilder" /> instance for method chaining.</returns>
    public CommandModuleBuilder Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : ICommand
    {
        Register(typeof(T));
        return this;
    }

    /// <summary>
    ///     Registers a command type for the message registry.
    /// </summary>
    /// <param name="type">The type of command to register, which must implement <see cref="IRegistrableCommandConstruct" />.</param>
    /// <returns>The current <see cref="CommandModuleBuilder" /> instance for method chaining.</returns>
    public CommandModuleBuilder Register([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        if (!type.IsAssignableTo(typeof(ICommand)))
        {
            throw new NotSupportedException($"The given type '{type.Name}' is not a command construct and cannot be registered.");
        }

        _messageRegistry.Register(type);
        return this;
    }

    /// <summary>
    ///     Registers pre-built handler descriptors, bypassing reflection-based descriptor
    ///     construction — the registration path used by source-generated code.
    /// </summary>
    /// <param name="descriptors">The descriptors to register; every handler type must be a command construct.</param>
    /// <returns>The current <see cref="CommandModuleBuilder" /> instance for method chaining.</returns>
    /// <exception cref="NotSupportedException">Thrown when a descriptor's handler type is not a command construct.</exception>
    public CommandModuleBuilder RegisterDescriptors(IEnumerable<IHandlerDescriptor> descriptors)
    {
        var accepted = new List<IHandlerDescriptor>();

        foreach (var descriptor in descriptors)
        {
            if (!descriptor.HandlerType.IsAssignableTo(typeof(ICommand)))
            {
                throw new NotSupportedException($"The given type '{descriptor.HandlerType.Name}' is not a command construct and cannot be registered.");
            }

            accepted.Add(descriptor);
        }

        _messageRegistry.RegisterDescriptors(accepted);
        return this;
    }

    /// <summary>
    ///     Registers all command types from the specified assembly that implement <see cref="IRegistrableCommandConstruct" />.
    /// </summary>
    /// <param name="assembly">The assembly from which to register command types.</param>
    /// <returns>The current <see cref="CommandModuleBuilder" /> instance for method chaining.</returns>
    [RequiresUnreferencedCode("Assembly scanning discovers command types via reflection; trimming may remove them. Register commands explicitly (or use source-generated registration) in trimmed or AOT applications.")]
    public CommandModuleBuilder RegisterFromAssembly(Assembly assembly)
    {
        foreach (var registrableCommandConstruct in assembly.GetTypes().Where(t => t.IsAssignableTo(typeof(ICommand))))
        {
            _messageRegistry.Register(registrableCommandConstruct);
        }

        return this;
    }
}