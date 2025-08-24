using System.Reflection;
using Ergosfare.Core.Abstractions.Registry;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

public class CoreModuleBuilder(IMessageRegistry registry): IModuleBuilder
{
    public IModuleBuilder Register(Type type)
    {
        registry.Register(type);
        return this;
    }


    public IModuleBuilder Register<T>()
    {
        Register(typeof(T));
        return this;
    }

    public IModuleBuilder RegisterFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            registry.Register(type);
        }
        return this;
    }
}