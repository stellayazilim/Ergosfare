using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Ergosfare.Examples.PluginExample;


// attach the module ergosfares module builder system
public static class ModuleRegistryExtensions
{
    public static IModuleRegistry AddExamplePlugin(this IModuleRegistry registry, Action<ExamplePluginBuilder> builder)
    {
        return registry.Register(new ExamplePlugin(builder));
    }
}