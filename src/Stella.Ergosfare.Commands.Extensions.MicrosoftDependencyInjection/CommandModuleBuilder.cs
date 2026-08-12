using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;

namespace Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;


/// <summary>
///     Builder class for selecting the command constructs this container runs from the
///     compiled composition table.
/// </summary>
public sealed class CommandModuleBuilder
{
    private readonly FrozenCompositionCatalog _compositions;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CommandModuleBuilder" /> class.
    /// </summary>
    /// <param name="compositions">
    ///     The container's composition catalog, told which constructs this registration
    ///     selects.
    /// </param>
    public CommandModuleBuilder(FrozenCompositionCatalog compositions)
    {
        _compositions = compositions ?? throw new ArgumentNullException(nameof(compositions));
    }

    /// <summary>
    ///     Registers a command construct.
    /// </summary>
    /// <typeparam name="T">The type to register, which must be a command construct.</typeparam>
    /// <returns>The current <see cref="CommandModuleBuilder" /> instance for method chaining.</returns>
    public CommandModuleBuilder Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : ICommand
    {
        Register(typeof(T));
        return this;
    }

    /// <summary>
    ///     Registers a command construct — a command, or one of the handlers and
    ///     interceptors serving commands (their contracts carry the module marker too).
    /// </summary>
    /// <param name="type">The type to register, which must be a command construct.</param>
    /// <returns>The current <see cref="CommandModuleBuilder" /> instance for method chaining.</returns>
    public CommandModuleBuilder Register([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        if (!type.IsAssignableTo(typeof(ICommand)))
        {
            throw new NotSupportedException($"The given type '{type.Name}' is not a command construct and cannot be registered.");
        }

        _compositions.Select(type);
        return this;
    }

    /// <summary>
    ///     Registers a batch of pipeline participants — the bulk path source-generated
    ///     registration uses.
    /// </summary>
    /// <remarks>
    ///     No module assertion here: the generator has already partitioned its discoveries
    ///     by module, and not every participant contract carries the module marker (the
    ///     modifying interceptor shapes are declared purely over the core contracts).
    ///     <see cref="Register(Type)" /> keeps the assertion, since a hand-written
    ///     registration is where a wrong-module type actually surfaces.
    /// </remarks>
    /// <param name="participantTypes">The handler and interceptor types to register.</param>
    /// <returns>The current <see cref="CommandModuleBuilder" /> instance for method chaining.</returns>
    public CommandModuleBuilder RegisterParticipants(IEnumerable<Type> participantTypes)
    {
        _compositions.Select(participantTypes);
        return this;
    }
}
