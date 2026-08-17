using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;

namespace Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;


/// <summary>
/// Selects which of the compiled command constructs this container runs.
/// </summary>
public sealed class CommandModuleBuilder
{
    private readonly FrozenCompositionCatalog _compositions;

    /// <summary>
    /// Initializes the builder over the container's composition catalog.
    /// </summary>
    /// <param name="compositions">
    /// The catalog this builder records the container's selection in.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="compositions"/> is <c>null</c>.</exception>
    public CommandModuleBuilder(FrozenCompositionCatalog compositions)
    {
        _compositions = compositions ?? throw new ArgumentNullException(nameof(compositions));
    }

    /// <summary>
    /// Registers one command construct.
    /// </summary>
    /// <typeparam name="T">
    /// The type to register: a command, or one of the handlers and interceptors that serve
    /// commands.
    /// </typeparam>
    /// <returns>The same builder, so calls can be chained.</returns>
    public CommandModuleBuilder Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : ICommand
    {
        Register(typeof(T));
        return this;
    }

    /// <summary>
    /// Registers one command construct.
    /// </summary>
    /// <param name="type">
    /// The type to register: a command, or one of the handlers and interceptors that serve
    /// commands — their contracts carry the module's marker too.
    /// </param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="NotSupportedException">
    /// The type does not belong to the command module.
    /// </exception>
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
    /// Registers many participants at once — the path generated registration uses.
    /// </summary>
    /// <param name="participantTypes">The handler and interceptor types to register.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// Unlike <see cref="Register(Type)"/> this does not check the module: the generator has
    /// already sorted its discoveries by module, and not every participant contract carries
    /// the marker — the message-replacing interceptor shapes are declared over the core
    /// contracts alone. The check stays on the single-type overload, where a hand-written
    /// registration of the wrong module is what it would catch.
    /// </remarks>
    public CommandModuleBuilder RegisterParticipants(IEnumerable<Type> participantTypes)
    {
        _compositions.Select(participantTypes);
        return this;
    }
}
