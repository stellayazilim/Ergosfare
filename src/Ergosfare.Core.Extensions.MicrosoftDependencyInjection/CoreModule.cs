namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

public sealed class CoreModule(Action<IModuleBuilder> builder): IModule
{
    public void Build(IModuleConfiguration configuration)
    {
        builder(new CoreModuleBuilder(configuration.MessageRegistry));
    }
}