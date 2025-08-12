using Ergosfare.Core.Abstractions.Registry;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

public class MessageModuleBuilder(IMessageRegistry registtry)
{
    private readonly IMessageRegistry _messageRegistry = registtry;

    
    
    public MessageModuleBuilder Register<T>()
    {
        _messageRegistry.Register(typeof(T));

        return this;
    }
   
}