namespace Ergosfare.Messaging.Extensions.MicrosoftDependencyInjection;

public sealed class MessageModule(Action<MessageModuleBuilder> builder): IModule
{
    public void Build(IModuleConfiguration configuration)
    {
        throw new NotImplementedException();
    }
}