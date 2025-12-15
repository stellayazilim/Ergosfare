using System.Reflection;

namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Provides a builder interface for registering types into a module.
/// Supports registration of individual types and bulk registration from assemblies.
/// </summary>
public interface IModuleBuilder
{

    /// <summary>
    /// Registers the specified generic type with the module.
    /// </summary>
    /// <typeparam name="T">
    /// The type to be registered.
    /// </typeparam>
    /// <returns>
    /// The current <see cref="IModuleBuilder"/> instance to allow for fluent chaining.
    /// </returns>
    IModuleBuilder Register<T>();

    /// <summary>
    /// Registers the specified type with the module.
    /// </summary>
    /// <param name="type">
    /// The <see cref="Type"/> to be registered. 
    /// Must not be <c>null</c>.
    /// </param>
    /// <returns>
    /// The current <see cref="IModuleBuilder"/> instance to allow for fluent chaining.
    /// </returns>
    IModuleBuilder Register(Type type);

    /// <summary>
    /// Registers all valid types from the provided assembly with the module.
    /// </summary>
    /// <param name="assembly">
    /// The <see cref="Assembly"/> to scan for types to register.
    /// Must not be <c>null</c>.
    /// </param>
    /// <returns>
    /// The current <see cref="IModuleBuilder"/> instance to allow for fluent chaining.
    /// </returns>
    IModuleBuilder RegisterFromAssembly(Assembly assembly);
}