using System.Reflection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection.Test;

/// <summary>
/// Contains unit tests for <see cref="IModuleBuilder"/> implementations,
/// verifying that registration works correctly both with generic and non-generic methods.
/// </summary>
public class IModuleBuilderTests
{
    /// <summary>
    /// A test implementation of <see cref="IModuleBuilder"/> used for verifying registration behavior.
    /// </summary>
    private class TestModuleBuilder: IModuleBuilder
    {
        /// <summary>
        /// Gets the last registered module type.
        /// </summary>
        public Type? ModuleType {  get; private set;  }
        
        /// <summary>
        /// Registers a module by its type.
        /// </summary>
        /// <param name="type">The module type to register.</param>
        /// <returns>The current builder instance for chaining.</returns>
        public IModuleBuilder Register(Type type)
        {
            ModuleType = type;
            return this;
        }

        /// <summary>
        /// Registers a module using a generic type parameter.
        /// </summary>
        /// <typeparam name="T">The type of module to register.</typeparam>
        /// <returns>The current builder instance for chaining.</returns>
        public IModuleBuilder Register<T>()
        {
            Register(typeof(T));
            return this;
        }

        /// <summary>
        /// Registers modules from a specified assembly.
        /// </summary>
        /// <param name="assembly">The assembly from which to register modules.</param>
        /// <returns>The current builder instance for chaining.</returns>
        public IModuleBuilder RegisterFromAssembly(Assembly assembly)
        {
            return this;
        }
    }

    /// <summary>
    /// Tests that the <see cref="IModuleBuilder.Register{T}"/> method correctly registers a module type using a generic type parameter.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShoudRegisterWithT()
    {
        // arrange 
        var builder = new TestModuleBuilder();
        
        // act 
        builder.Register<IMessage>();
        
        // assert
        Assert.Equal(typeof(IMessage), builder.ModuleType!);
    }
    
    
    
    /// <summary>
    /// Tests that the <see cref="IModuleBuilder.Register(Type)"/> method correctly registers a module type using a Type parameter.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShoudRegisterWithoutT()
    {
        // arrange 
        var builder = new TestModuleBuilder();
        
        // act 
        builder.Register(typeof(IMessage));
        
        // assert
        Assert.Equal(typeof(IMessage), builder.ModuleType!);
    }
}