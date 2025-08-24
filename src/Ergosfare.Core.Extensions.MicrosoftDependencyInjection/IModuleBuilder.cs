using System.Reflection;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

public interface IModuleBuilder
{

    IModuleBuilder Register<T>();

    
    IModuleBuilder Register(Type type);

    IModuleBuilder RegisterFromAssembly(Assembly assembly);
}