using System.Reflection;
using Ergosfare.Core.Abstractions;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection.Test;

public class IModuleBuilderTests
{
    private class TestModuleBuilder: IModuleBuilder
    {
        
        public Type? ModuleType {  get; private set;  }
        public IModuleBuilder Register(Type type)
        {
            ModuleType = type;
            return this;
        }

        public IModuleBuilder Register<T>()
        {
            Register(typeof(T));
            return this;
        }

        public IModuleBuilder RegisterFromAssembly(Assembly assembly)
        {
            return this;
        }
    }

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