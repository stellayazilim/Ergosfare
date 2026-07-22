using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Stella.Ergosfare.Core.Abstractions.Registry;

namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Provides a fluent API for registering message types into the <see cref="IMessageRegistry"/>.
/// </summary>
/// <remarks>
/// The <see cref="CoreModuleBuilder"/> is typically used inside the <see cref="CoreModule"/> 
/// to configure which types should be recognized by the framework as messages (commands, queries, events, etc.).
/// </remarks>
public class CoreModuleBuilder(IMessageRegistry registry): IModuleBuilder
{
    
    /// <summary>
    /// Registers the specified type into the <see cref="IMessageRegistry"/>.
    /// </summary>
    /// <param name="type">The type to register as a message.</param>
    /// <returns>The current <see cref="IModuleBuilder"/> instance for fluent chaining.</returns>
    public IModuleBuilder Register([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        registry.Register(type);
        return this;
    }


    /// <summary>
    /// Registers the specified generic type <typeparamref name="T"/> into the <see cref="IMessageRegistry"/>.
    /// </summary>
    /// <typeparam name="T">The type to register as a message.</typeparam>
    /// <returns>The current <see cref="IModuleBuilder"/> instance for fluent chaining.</returns>
    public IModuleBuilder Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
    {
        Register(typeof(T));
        return this;
    }
    
    
    /// <summary>
    /// Registers all types from the given assembly into the <see cref="IMessageRegistry"/>.
    /// </summary>
    /// <param name="assembly">The assembly whose types should be registered as messages.</param>
    /// <returns>The current <see cref="IModuleBuilder"/> instance for fluent chaining.</returns>

    [RequiresUnreferencedCode("Assembly scanning discovers types via reflection; trimming may remove them. Register types explicitly (or use source-generated registration) in trimmed or AOT applications.")]
    public IModuleBuilder RegisterFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            registry.Register(type);
        }
        return this;
    }
}