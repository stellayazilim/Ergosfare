using Ergosfare.Messaging.Abstractions.Registry;

namespace Ergosfare.Messaging.Extensions.MicrosoftDependencyInjection;

public class MessageModuleBuilder(IMessageRegistry registtry)
{
    private readonly IMessageRegistry _messageRegistry = registtry;

    
    
    public MessageModuleBuilder Register<T>()
    {
        _messageRegistry.Register(typeof(T));

        return this;
    }
   
}