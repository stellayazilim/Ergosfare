using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Examples.PluginExample;


internal class ExamplePlugin : IModule
{
    private readonly Action<ExamplePluginBuilder> _builderAction;

    public ExamplePlugin(Action<ExamplePluginBuilder> builderAction)
    {
        _builderAction = builderAction;
    }

    public void Build(IModuleConfiguration configuration)
    {
        var builder = new ExamplePluginBuilder();
        _builderAction(builder);

        if (builder.EnableHelloWorld)
        {
            configuration.Services.AddSingleton<ExampleService>();
        }
    }
}